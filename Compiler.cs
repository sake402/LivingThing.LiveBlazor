using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LivingThing.LiveBlazor
{
    internal class Compiler
    {
        static MetadataReference[] References;
        static Compiler()
        {
            References = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location)).Select(a => MetadataReference.CreateFromFile(a.Location)).ToArray();
        }

        public byte[] Compile(params string[] sourceCodes)
        {
            //Console.WriteLine($"Starting compilation of: '{file}'");

            //var sourceCode = fileIsCode ? file : File.ReadAllText(file);

            using (var peStream = new MemoryStream())
            {
                var result = GenerateCode(sourceCodes).Emit(peStream);

                if (!result.Success)
                {
                    Console.WriteLine("Compilation done with error.");

                    var failures = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);

                    foreach (var diagnostic in failures)
                    {
                        Console.Error.WriteLine("{0}: {1}", diagnostic.Id, diagnostic.GetMessage());
                    }

                    return null;
                }

                Console.WriteLine("Compilation done without any error.");

                peStream.Seek(0, SeekOrigin.Begin);

                return peStream.ToArray();
            }
        }

        int dllNumber;
        private CSharpCompilation GenerateCode(params string[] sourceCode)
        {
            List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();
            foreach (var code in sourceCode)
            {
                //var codeString =/* SourceText.From*/(sourceCode);
                var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp8);

                var parsedSyntaxTree = SyntaxFactory.ParseSyntaxTree(code, options);
                syntaxTrees.Add(parsedSyntaxTree);
            }

            dllNumber++;
            return CSharpCompilation.Create($"_{dllNumber}.dll",
                syntaxTrees.ToArray(),
                references: References,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release,
                    assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default));
        }
    }
}