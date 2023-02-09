using System.IO.Pipelines;

namespace FeatBit.Sdk.Server.Transport
{
    internal sealed class DuplexPipe : IDuplexPipe
    {
        public DuplexPipe(PipeReader reader, PipeWriter writer)
        {
            Input = reader;
            Output = writer;
        }

        public PipeReader Input { get; }

        public PipeWriter Output { get; }

        public static DuplexPipePair CreateConnectionPair(PipeOptions transportPipeOptions, PipeOptions appPipeOptions)
        {
            var transport = new Pipe(transportPipeOptions);
            var application = new Pipe(appPipeOptions);

            var transportToApplication = new DuplexPipe(application.Reader, transport.Writer);
            var applicationToTransport = new DuplexPipe(transport.Reader, application.Writer);

            return new DuplexPipePair(applicationToTransport, transportToApplication);
        }

        // This class exists to work around issues with value tuple on .NET Framework
        public readonly struct DuplexPipePair
        {
            public IDuplexPipe Transport { get; }
            public IDuplexPipe Application { get; }

            public DuplexPipePair(IDuplexPipe transport, IDuplexPipe application)
            {
                Transport = transport;
                Application = application;
            }
        }
    }
}