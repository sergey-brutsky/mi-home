using System;
using FluentAssertions;
using MiHomeLib.Helpers;
using Moq;
using Xunit;

namespace MiHomeUnitTests.Protocols;

public class ResourceFactoryTests
{
    [Fact]
    public void Cleanup_Failure_Does_Not_Replace_The_Creation_Failure()
    {
        var failure = new InvalidOperationException("creation failed");
        var cleanupFailure = new Exception("cleanup failed");
        var first = new Mock<IDisposable>();
        first.Setup(resource => resource.Dispose()).Throws(cleanupFailure);

        Action create = () => ResourceFactory.CreatePair<IDisposable, IDisposable>(() => first.Object, () => throw failure);

        var error = create.Should().ThrowExactly<InvalidOperationException>().Which;
        error.Should().BeSameAs(failure);
        error.Data["ResourceCleanupException"].Should().BeSameAs(cleanupFailure);
        first.Verify(resource => resource.Dispose(), Times.Once);
    }

    [Fact]
    public void Failed_Second_Resource_Disposes_First()
    {
        var first = new Mock<IDisposable>();
        Action create = () => ResourceFactory.CreatePair<IDisposable, IDisposable>(() => first.Object,
            () => throw new InvalidOperationException("connection failed"));
        create.Should().Throw<InvalidOperationException>().WithMessage("connection failed");
        first.Verify(resource => resource.Dispose(), Times.Once);
    }

    [Fact]
    public void Successful_Pair_Transfers_Ownership_To_Caller()
    {
        var first = new Mock<IDisposable>();
        var second = new Mock<IDisposable>();
        var pair = ResourceFactory.CreatePair(() => first.Object, () => second.Object);
        pair.First.Should().BeSameAs(first.Object);
        pair.Second.Should().BeSameAs(second.Object);
        first.Verify(resource => resource.Dispose(), Times.Never);
        second.Verify(resource => resource.Dispose(), Times.Never);
    }
}
