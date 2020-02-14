using System;

namespace Skylight.Sdk.Tests
{
    abstract class APITest
    {
        public APITest() {

        }

        public void Run() {
            InnerRun();
        }

        protected abstract void InnerRun();
    }
}
