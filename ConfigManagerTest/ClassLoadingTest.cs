using System;
using System.Collections.Generic;
using System.Linq;
using ConfigManager;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConfigManagerTest
{
    [TestClass]
    public class ClassLoadingTest
    {
        private class ClassSimple
        {
            public string name;
            public int value;
            
            public override bool Equals(object obj)
            {
                if (obj is ClassSimple r)
                {
                    return string.Equals(name, r.name) && value == r.value;
                }
                return false;
            }
        }

        private class ClassSelfContained
        {
            public string value;
            public ClassSelfContained contained;

            public override bool Equals(object obj)
            {
                if (obj is ClassSelfContained r)
                {
                    return value == r.value
                           && ((contained == null && r.contained == null)
                               || (contained?.Equals(r.contained) ?? false));
                }
                return false;
            }
        }

        private class ClassComplex
        {
            public DateTime time;
            public int i;
            public long l;
            public string s;
            public double d;

            public override bool Equals(object obj)
            {
                if (obj is ClassComplex r)
                {
                    return time == r.time
                           && i == r.i
                           && l == r.l
                           && s == r.s
                           && Math.Abs(d - r.d) <= double.Epsilon;
                }
                return false;
            }
        }
        
        [TestMethod]
        public void TestLoadSimple()
        {
            var expected = new ClassSimple
            {
                name = "Test method",
                value = 15210
            };
            
            var actual = ConfigTestTools.LoadValidConfig<ClassSimple>(
                Config.SaveToString(Config.ConvertFromClass(expected))
            );
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void TestLoadSelfContained()
        {
            var expected = new ClassSelfContained
            {
                value = "Root element",
                contained = new ClassSelfContained
                {
                    value = "Inner class",
                    contained = new ClassSelfContained
                    {
                        value = "Deeply placed container.",
                        contained = null
                    }
                }
            };
            
            var actual = ConfigTestTools.LoadValidConfig<ClassSelfContained>(
                Config.SaveToString(Config.ConvertFromClass(expected))
            );
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void TestComplexValues()
        {
            var expected = new ClassComplex
            {
                time = DateTime.MinValue.AddDays(1234),
                i = 1125,
                l = 962377529306543236,
                s = "This\nis\ta{TEST} \"of\" parser's capabilities",
                d = 256.125
            };
            
            var actual = ConfigTestTools.LoadValidConfig<ClassComplex>(
                Config.SaveToString(Config.ConvertFromClass(expected))
            );
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void TestListOfSimple()
        {
            var expected = new List<ClassSimple>
            {
                new ClassSimple
                {
                    name = "Test value #1",
                    value = 12345
                },
                new ClassSimple
                {
                    name = "It should be on a second place",
                    value = 54321
                },
                new ClassSimple
                {
                    name = "Last element. Ordering is important",
                    value = 777
                }
            };
            var actual = ConfigTestTools.LoadValidConfig<List<ClassSimple>>(
                Config.SaveToString(Config.ConvertFromClass(expected))
            );
            Assert.IsTrue(expected.SequenceEqual(actual));
        }
    }
}