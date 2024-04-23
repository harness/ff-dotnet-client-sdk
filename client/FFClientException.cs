using System;

namespace io.harness.ff_dotnet_client_sdk.client
{
    public class FFClientException : Exception
    {
        public FFClientException(string  errorMessage) : base(errorMessage)
        {
          
        }

        public FFClientException(string  errorMessage, Exception innerException) : base(errorMessage, innerException)
        {
        }
    }
}