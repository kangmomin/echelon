using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class SimHashTests
    {
        [Test]
        public void VectorHashMatchesSpecifiedPortableFnv1a()
        {
            Assert.That(SimHash.Hash(new SimVec2(1.0, 2.0)), Is.EqualTo(682974765188841944L));
        }

        [Test]
        public void IntegerVectorHashIsRepeatableAndOrderSensitive()
        {
            SimVec2I value = new SimVec2I(17, -23);
            Assert.That(SimHash.Hash(value), Is.EqualTo(SimHash.Hash(new SimVec2I(17, -23))));
            Assert.That(SimHash.Hash(value), Is.Not.EqualTo(SimHash.Hash(new SimVec2I(-23, 17))));
        }
    }
}
