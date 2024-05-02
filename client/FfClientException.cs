using System;

namespace io.harness.ff_dotnet_client_sdk.client
{
    public class FfClientException : Exception
    {
        public FfClientException(string  errorMessage) : base(errorMessage)
        {
          
        }

        public FfClientException(string  errorMessage, Exception innerException) : base(errorMessage, innerException)
        {
        }
    }
}