using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace LivingThing.LiveBlazor
{
    internal class UnloadableAssemblyLoadContext : AssemblyLoadContext
    {
        public UnloadableAssemblyLoadContext()
            : base(true)
        {
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            return null;
        }
    }
}
