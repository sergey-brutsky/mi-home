using System;
using System.Runtime.Serialization;

namespace MiHomeLib;

[Serializable]
public class ModelNotSupportedException : Exception
{
    public ModelNotSupportedException()
    {
    }

    public ModelNotSupportedException(string message) : base(message)
    {
    }

    public ModelNotSupportedException(string message, Exception innerException) : base(message, innerException)
    {
    }

    protected ModelNotSupportedException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}