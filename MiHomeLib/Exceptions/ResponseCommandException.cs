using System;
using System.Runtime.Serialization;

namespace MiHomeLib.Commands
{
    [Serializable]
    internal class ResponseCommandException : Exception
    {
        private Exception e;

        
        public ResponseCommandException(Exception e)
        {
            this.e = e;
        }

        public ResponseCommandException(string message) : base(message)
        {
        }

        public ResponseCommandException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected ResponseCommandException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}