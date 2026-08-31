#!/usr/bin/env node

import { spawn, spawnSync } from 'node:child_process';
import { appendFileSync, createWriteStream, mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import process from 'node:process';

const repoRoot = resolve(import.meta.dirname);
const runId = new Date().toISOString().replaceAll(/[-:TZ.]/g, '').slice(0, 14);
const artifactRoot = join(repoRoot, 'artifacts', 'els-reconnect');
const artifactDirectory = join(artifactRoot, runId);
const clientProjectDirectory = join(artifactDirectory, 'sdk-client');
const sdkProject = join(repoRoot, 'src', 'FeatBit.ServerSdk', 'FeatBit.ServerSdk.csproj');
const elsBaseUri = 'http://localhost:5100';
const postgresContainer = 'featbit-infra-postgresql-1';
const elsContainer = 'featbit-sdk-reconnect-els';
const elsImage = 'featbit/evaluation-server:reconnect-comparison';
const dockerNetwork = 'featbit-network';

const options = parseArguments(process.argv.slice(2));
mkdirSync(artifactRoot, { recursive: true });
mkdirSync(artifactDirectory, { recursive: true });
const testLogPath = join(artifactRoot, `sdk-${runId}-test.log`);
const clientLogPath = join(artifactRoot, `sdk-${runId}-client.log`);

let environmentSecret = '';
let clientProcess = null;
let clientStatus = '';
let issueReproduced = false;
let testStarted = false;
let sdkClosedAt = null;
let clientStoppedAt = null;

function parseArguments(args) {
  const parsed = {
    environmentId: '', testDurationSeconds: 300, downtimeSeconds: 2,
    recoveryTimeoutSeconds: 90, closedConfirmationSeconds: 10,
    stopElsAfterTest: false, keepArtifacts: false,
  };
  for (let index = 0; index < args.length; index += 1) {
    const argument = args[index];
    const next = () => {
      index += 1;
      if (index >= args.length) throw new Error(`Missing value for ${argument}.`);
      return args[index];
    };
    switch (argument) {
      case '--environment-id': parsed.environmentId = next(); break;
      case '--test-duration': parsed.testDurationSeconds = positiveInteger(next(), argument); break;
      case '--downtime': parsed.downtimeSeconds = nonNegativeInteger(next(), argument); break;
      case '--recovery-timeout': parsed.recoveryTimeoutSeconds = positiveInteger(next(), argument); break;
      case '--closed-confirmation': parsed.closedConfirmationSeconds = positiveInteger(next(), argument); break;
      case '--stop-els-after-test': parsed.stopElsAfterTest = true; break;
      case '--keep-artifacts': parsed.keepArtifacts = true; break;
      case '--help': printHelp(); process.exit(0);
      default: throw new Error(`Unknown argument: ${argument}`);
    }
  }
  return parsed;
}

function positiveInteger(value, option) {
  const number = Number.parseInt(value, 10);
  if (!Number.isInteger(number) || number <= 0) throw new Error(`${option} must be a positive integer.`);
  return number;
}

function nonNegativeInteger(value, option) {
  const number = Number.parseInt(value, 10);
  if (!Number.isInteger(number) || number < 0) throw new Error(`${option} must be a non-negative integer.`);
  return number;
}

function printHelp() {
  console.log(`Usage: node reconnect-test.mjs [options]

Builds a temporary client against the SDK source in this checkout, repeatedly restarts ELS,
and detects whether FbClient becomes permanently Closed.

Options:
  --environment-id <id>       Environment whose server secret is used (default: first environment)
  --test-duration <seconds>   Total restart-test interval (default: 300)
  --downtime <seconds>        ELS downtime per cycle (default: 2)
  --recovery-timeout <secs>   Maximum Ready-state recovery wait per cycle (default: 90)
  --closed-confirmation <s>   Time Closed must persist after ELS recovery (default: 10)
  --stop-els-after-test       Leave ELS stopped after the test
  --keep-artifacts            Keep the generated temporary client project
  --help                      Show this help

Exit codes: 0 = no stuck connection observed, 2 = permanent Closed state reproduced.
`);
}

function step(message) { console.log(`\n==> ${message}`); }
function sleep(milliseconds) { return new Promise((resolvePromise) => setTimeout(resolvePromise, milliseconds)); }

function timestamp(date = new Date()) {
  const pad = (value, length = 2) => String(value).padStart(length, '0');
  const offsetMinutes = -date.getTimezoneOffset();
  const sign = offsetMinutes >= 0 ? '+' : '-';
  const offset = Math.abs(offsetMinutes);
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ` +
    `${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}.` +
    `${pad(date.getMilliseconds(), 3)} ${sign}${pad(Math.floor(offset / 60))}:${pad(offset % 60)}`;
}

function logTest(message, date = new Date()) {
  const line = `${timestamp(date)} [TEST] ${message}`;
  console.log(line);
  appendFileSync(testLogPath, `${line}\n`, 'utf8');
}

function run(command, args, { cwd = repoRoot, allowFailure = false } = {}) {
  const result = spawnSync(command, args, { cwd, encoding: 'utf8', windowsHide: true });
  if (result.error) throw result.error;
  if (!allowFailure && result.status !== 0) {
    throw new Error(`${command} ${args.join(' ')} failed.\n${result.stdout ?? ''}${result.stderr ?? ''}`);
  }
  return result;
}

function docker(args, settings = {}) { return run('docker', args, settings); }
function escapeSqlLiteral(value) { return value.replaceAll("'", "''"); }
function queryDatabase(sql) {
  return docker(['exec', postgresContainer, 'psql', '-U', 'postgres', '-d', 'featbit', '-Atc', sql])
    .stdout.trim().split(/\r?\n/)[0]?.trim() ?? '';
}

async function waitFor(description, predicate, timeoutSeconds) {
  const deadline = Date.now() + timeoutSeconds * 1_000;
  while (Date.now() < deadline) {
    if (await predicate()) return;
    await sleep(250);
  }
  throw new Error(`${description} timed out after ${timeoutSeconds} seconds (last SDK status: ${clientStatus || 'Unavailable'}).`);
}

async function endpointStatus(uri) {
  try { return (await fetch(uri, { signal: AbortSignal.timeout(2_000) })).status; }
  catch { return 0; }
}

function resolveEnvironment() {
  const idFilter = options.environmentId
    ? `and e.id::text = '${escapeSqlLiteral(options.environmentId)}'`
    : '';
  const row = queryDatabase(`select concat_ws('|', e.id::text, e.name, s.item->>'value')
    from environments e cross join lateral jsonb_array_elements(e.secrets) s(item)
    where s.item->>'type' = 'server' ${idFilter}
    order by e.created_at, e.id limit 1;`);
  const first = row.indexOf('|');
  const second = row.indexOf('|', first + 1);
  if (first < 1 || second < 0 || second === row.length - 1) {
    throw new Error(options.environmentId
      ? `No server secret found for environment '${options.environmentId}'.`
      : 'No environment server secret was found.');
  }
  console.log(`Environment: ${row.slice(first + 1, second)} (${row.slice(0, first)})`);
  environmentSecret = row.slice(second + 1);
}

function ensureElsContainer() {
  if (docker(['inspect', elsContainer], { allowFailure: true }).status === 0) {
    docker(['start', elsContainer]);
    return;
  }
  docker([
    'run', '--detach', '--name', elsContainer, '--network', dockerNetwork, '--publish', '5100:5100',
    '--env', 'ASPNETCORE_ENVIRONMENT=Production', '--env', 'DbProvider=Postgres',
    '--env', 'MqProvider=Postgres', '--env', 'CacheProvider=None',
    '--env', 'Postgres__ConnectionString=Host=postgresql;Port=5432;Username=postgres;Password=please_change_me;Database=featbit',
    elsImage,
  ]);
}

async function waitForEls() {
  await waitFor('ELS health check', async () => await endpointStatus(`${elsBaseUri}/health/liveness`) === 200, 90);
}

function createClientProject() {
  mkdirSync(clientProjectDirectory, { recursive: true });
  writeFileSync(join(clientProjectDirectory, 'ReconnectClient.csproj'), `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup><ProjectReference Include="${sdkProject.replaceAll('\\', '/')}" /></ItemGroup>
</Project>
`, 'utf8');
  writeFileSync(join(clientProjectDirectory, 'Program.cs'), `using FeatBit.Sdk.Server;
using FeatBit.Sdk.Server.Options;

var secret = Environment.GetEnvironmentVariable("FEATBIT_RECONNECT_SECRET")
    ?? throw new InvalidOperationException("FEATBIT_RECONNECT_SECRET is required.");
var options = new FbOptionsBuilder(secret)
    .Streaming(new Uri("ws://localhost:5100"))
    .Event(new Uri("http://localhost:5100"))
    .DisableEvents(true)
    .StartWaitTime(TimeSpan.FromSeconds(10))
    .Build();
var client = new FbClient(options);
using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
FbClientStatus? lastStatus = null;
while (await timer.WaitForNextTickAsync())
{
    if (lastStatus != client.Status)
    {
        lastStatus = client.Status;
        Console.WriteLine(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz") + " [SDK] STATUS=" + lastStatus);
    }
}
`, 'utf8');
}

function observeClientOutput(chunk, log) {
  const text = chunk.toString();
  log.write(text);
  for (const match of text.matchAll(/STATUS=(NotReady|Ready|Stale|Closed)/g)) {
    if (match[1] !== clientStatus) {
      clientStatus = match[1];
      logTest(`SDK status changed to ${clientStatus}.`);
      if (clientStatus === 'Closed' && sdkClosedAt === null) sdkClosedAt = new Date();
    }
  }
}

function startClient() {
  createClientProject();
  run('dotnet', ['build', 'ReconnectClient.csproj', '--configuration', 'Release'], { cwd: clientProjectDirectory });
  const assembly = join(clientProjectDirectory, 'bin', 'Release', 'net8.0', 'ReconnectClient.dll');
  const log = createWriteStream(clientLogPath);
  const child = spawn('dotnet', [assembly], {
    cwd: clientProjectDirectory, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'],
    env: { ...process.env, FEATBIT_RECONNECT_SECRET: environmentSecret },
  });
  child.stdout.on('data', (chunk) => observeClientOutput(chunk, log));
  child.stderr.on('data', (chunk) => observeClientOutput(chunk, log));
  clientProcess = { child, log };
  console.log(`SDK client log: ${clientLogPath}`);
}

async function stopClient(reason) {
  if (!clientProcess) return;
  clientStoppedAt = new Date();
  logTest(`Stopping SDK client process (${reason}).`, clientStoppedAt);
  clientProcess.child.kill();
  await Promise.race([
    new Promise((resolvePromise) => clientProcess.child.once('exit', resolvePromise)), sleep(5_000),
  ]);
  clientProcess.log.end();
  clientProcess = null;
  logTest('SDK client process stop completed.');
}

async function confirmClosed(cycle) {
  if (clientStatus !== 'Closed') return false;
  await sleep(options.closedConfirmationSeconds * 1_000);
  if (clientStatus !== 'Closed') return false;
  issueReproduced = true;
  logTest(`Cycle ${cycle}: SDK remained Closed ${options.closedConfirmationSeconds} seconds after ELS recovered.`);
  return true;
}

async function runRestartTest() {
  const deadline = Date.now() + options.testDurationSeconds * 1_000;
  let cycle = 0;
  while (Date.now() < deadline) {
    cycle += 1;
    logTest(`Cycle ${cycle}: stopping ELS.`);
    docker(['stop', '--time', '10', elsContainer]);
    await waitFor('ELS shutdown', async () => await endpointStatus(`${elsBaseUri}/health/liveness`) === 0, 15);
    logTest(`Cycle ${cycle}: ELS is stopped; SDK status is ${clientStatus || 'Unavailable'}.`);
    await sleep(options.downtimeSeconds * 1_000);
    docker(['start', elsContainer]);
    await waitForEls();
    logTest(`Cycle ${cycle}: ELS is healthy again; SDK status is ${clientStatus || 'Unavailable'}.`);
    if (await confirmClosed(cycle)) return;
    await waitFor(`Cycle ${cycle} SDK recovery`,
      async () => clientStatus === 'Ready' || clientStatus === 'Closed', options.recoveryTimeoutSeconds);
    if (await confirmClosed(cycle)) return;
    logTest(`Cycle ${cycle}: SDK reconnected and returned to Ready.`);
  }
  logTest(`Completed ${cycle} restart cycles within the requested test interval.`);
}

try {
  step('Resolving test configuration');
  for (const command of ['node', 'dotnet', 'docker']) run(command, ['--version']);
  resolveEnvironment();
  console.log(`SDK source: ${sdkProject}`);
  console.log(`Test time: ${options.testDurationSeconds} seconds`);
  console.log(`Temporary files: ${artifactDirectory}`);

  step('Preparing SDK client and ELS');
  ensureElsContainer();
  await waitForEls();
  startClient();
  environmentSecret = '';
  await waitFor('SDK initial synchronization', async () => clientStatus === 'Ready', 90);

  step('Running ELS restart cycles');
  testStarted = true;
  await runRestartTest();
  step(issueReproduced ? 'Issue reproduced' : 'Test interval completed without reproducing the issue');
} finally {
  environmentSecret = '';
  await stopClient(issueReproduced ? 'after reproducing the reconnect issue' : 'after completing the test interval');
  if (issueReproduced) {
    logTest(`RESULT: Issue reproduced. SDK entered terminal Closed state at ${timestamp(sdkClosedAt)}.`);
  } else if (testStarted) {
    logTest('RESULT: Test completed without observing a persistent SDK Closed state.');
  } else {
    logTest('RESULT: Test aborted before the ELS restart phase.');
  }
  if (clientStoppedAt !== null) logTest(`RESULT: Test harness stopped the SDK client at ${timestamp(clientStoppedAt)}.`);
  if (!options.keepArtifacts) rmSync(artifactDirectory, { recursive: true, force: true });
  if (options.stopElsAfterTest) docker(['stop', elsContainer], { allowFailure: true });
  else {
    docker(['start', elsContainer], { allowFailure: true });
    await waitForEls();
  }
  console.log(`SDK client log: ${clientLogPath}`);
  console.log(`Test log: ${testLogPath}`);
}

if (issueReproduced) process.exitCode = 2;
