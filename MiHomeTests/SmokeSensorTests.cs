using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiHomeLib;

namespace MiHomeTests
{
    [TestClass]
    public class SmokeSensorTests
    {
        [TestMethod]
        public void TestMethod1()
        {
            using (var miHome = new MiHome())
            {
                Assert.IsTrue(1 == 1);
            }
        }
    }
}
