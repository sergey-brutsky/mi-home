using System;
using System.Runtime.Serialization;

namespace MiHomeLib.Exceptions;

[Serializable]
internal class ResponseCommandException : Exception
{
    private readonly Exception _ex;

    
    public ResponseCommandException(Exception e)
    {
        _ex = e;
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