using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HarmonyLib;
using Microsoft.AspNetCore.Components;
using RxFileSystemWatcher;

namespace LivingThing.LiveBlazor
{
    public class LiveConfiguration
    {
        public string RazoGeneratePath { get; set; }
        public string ProjectConfiguration { get; set; }
        public string WatchDirectory { get; set; }
    }
    internal class LiveComponentContext
    {
        public MethodInfo OriginalMethod { get; set; }
        public Type NewType { get; set; }
        public MethodInfo Replacer { get; set; }
        public List<ComponentBase> Components { get; set; } = new List<ComponentBase>();
    }

    internal class ProjectInfo
    {
        public string Path { get; set; }
        public string FileName { get; set; }
        public string Type { get; set; }
    }

    public static class Blazor
    {
        static Dictionary<Type, LiveComponentContext> liveContexts = new Dictionary<Type, LiveComponentContext>();

        public static void Prefix(ComponentBase __instance)
        {
            var type = __instance.GetType();
            if (!liveContexts.ContainsKey(type))
            {
                liveContexts[type] = new LiveComponentContext();
            }
            var contexts = liveContexts[type];
            if (!contexts.Components.Contains(__instance))
            {
                contexts.Components.Add(__instance);
            }
        }

        static Type currentType;
        public static IEnumerable<CodeInstruction> ReplaceBuildRenderTree/*<TComponent>*/(IEnumerable<CodeInstruction> instructions)
        {
            var context = liveContexts[currentType];//[typeof(TComponent)];
            Type newCompiledType = context.NewType;
            var newRenderTree = newCompiledType.GetMethod("BuildRenderTree", BindingFlags.NonPublic | BindingFlags.Instance);
            var newInstructions = newRenderTree.GetInstructions();
            var newCodeInstructions = newInstructions.Select(i =>
            {
                return new CodeInstruction(i.OpCode, i.Operand);
            }).ToArray();
            return newCodeInstructions;
        }

        static ProjectInfo GetProjectPath(string path)
        {
            string fileName = null;
            if (Directory.EnumerateFiles(path).Any(f =>
            {
                if (f.EndsWith(".csproj"))
                {
                    fileName = f;
                    return true;
                }
                return false;
            }))
            {
                var csproj = File.ReadAllText(fileName);
                var match = Regex.Match(csproj, ".?<TargetFramework>(.+)</TargetFramework>.?");
                string projectType = "netstandard2.1";
                if (match.Success)
                {
                    projectType = match.Groups[1].Value;
                }
                return new ProjectInfo()
                {
                    Path = Path.GetFullPath(path),
                    FileName = fileName,
                    Type = projectType
                };
            }
            path = path.Trim(new char[] {'/', '\\' }) + "/../";
            return GetProjectPath(path);
        }

        public static async Task Live(LiveConfiguration configuration = null)
        {
            //Extract LiveBlazor.zip to a temporary folter
            //var stream = typeof(LiveBlazor).Assembly.GetManifestResourceStream("LivingThing.LiveBlazor.LiveBlazor.zip");
            //var workingDirectory = Path.GetDirectoryName(Path.Combine(Environment.CurrentDirectory, "..", "LiveBlazor"));
            //if (!Directory.Exists(workingDirectory))
            //{
            //    Directory.CreateDirectory(workingDirectory);
            //}
            //var zipPath = Path.Combine(workingDirectory, "LiveBlazor.zip");
            //FileStream fs = new FileStream(zipPath, FileMode.Create);
            //stream.CopyTo(fs);
            //fs.Close();
            //stream.Close();
            //ZipFile.ExtractToDirectory(zipPath, workingDirectory, true);
            ////prebuild project enabling restore, so we dont have to restor anymore, which is faster
            //$"cd {workingDirectory} & dotnet build".Bash();

            string dotnetPath = (await "where dotnet".CLI()).StdOut.Trim();
            string dotnetVersion = (await "dotnet --version".CLI()).StdOut.Trim();
            var dotnetFolder = Path.GetDirectoryName(dotnetPath) + "\\";

            var harmony = new Harmony("com.liveblazor.livingthing");
            var invokeAsync = typeof(ComponentBase).GetMethod("InvokeAsync", bindingAttr:BindingFlags.NonPublic | BindingFlags.Instance, types:new Type[] { typeof(Action) }, binder:null, modifiers:null);
            var stateHasChanged = typeof(ComponentBase).GetMethod("StateHasChanged", BindingFlags.NonPublic | BindingFlags.Instance);

            //find all components in all assemblies
            var componentTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).Where(t => !t.IsAbstract && t.MemberType == MemberTypes.TypeInfo && typeof(ComponentBase).IsAssignableFrom(t)).ToArray();
            var prefix = typeof(Blazor).GetMethod(nameof(Prefix));
            foreach (var type in componentTypes)
            {
                var renderTree = type.GetMethod("BuildRenderTree", BindingFlags.NonPublic | BindingFlags.Instance);
                if (renderTree.DeclaringType == type)
                {
                    harmony.Patch(renderTree, new HarmonyMethod(prefix));
                }
            }

            string watchPath = configuration?.WatchDirectory ?? Environment.CurrentDirectory;
            if (configuration?.WatchDirectory == null)
            {
                var solutionPath = Path.GetFullPath(Path.Combine(watchPath, "../"));
                if (Directory.EnumerateFiles(solutionPath).Any(f=> f.EndsWith(".sln")))
                {
                    watchPath = solutionPath;
                }
            }

            var watcher = new ObservableFileSystemWatcher(c =>
            {
                c.Path = watchPath;
                c.IncludeSubdirectories = true;
                c.Filters.Add("*.razor");
            });

            var changes = watcher.Changed.Throttle(TimeSpan.FromSeconds(.5));

            changes.Subscribe(async filepath =>
            {
                string razorGeneratePath = configuration?.RazoGeneratePath ?? @$"{dotnetFolder}sdk\{dotnetVersion}\Sdks\Microsoft.NET.Sdk.Razor\tools\netcoreapp3.0\rzc.dll";
                var project = GetProjectPath(Path.GetDirectoryName(filepath.FullPath));
                string projectName = Path.GetFileNameWithoutExtension(project.FileName);
                var workspace = $"obj\\Debug\\{project.Type}\\";
                var workingDirectory = $"{project.Path}{workspace}";
                var outputPath = $"{workingDirectory}{Path.GetFileName(filepath.FullPath)}.g.cs";
                string filePathInProject = filepath.FullPath.Replace(project.Path, "");
                var @namespace = projectName;
                string compile = $"dotnet exec \"{razorGeneratePath}\" generate -s \"{filepath.FullPath}\" -r \"{filePathInProject}\" -o \"{outputPath}\" -k component -p {project.Path} -v 3.0 -c {configuration?.ProjectConfiguration??"Default"} --root-namespace {@namespace} -t \"{workspace}{projectName}.TagHelpers.output.cache\"";
                await $"cd {project.Path} & {compile}".CLI();

                var file = File.ReadAllText(outputPath);
                var csFile = Path.ChangeExtension(filepath.FullPath, ".cs");
                if (File.Exists(csFile))
                {
                    var csFileContent = File.ReadAllText(csFile);
                    file += "\r\n" + csFileContent;
                }


                var compiler = new Compiler();
                var code = compiler.Compile(file, true);

                using (var asm = new MemoryStream(code))
                {
                    var assemblyLoadContext = new UnloadableAssemblyLoadContext();

                    //var assembly = assemblyLoadContext.LoadFromStream(asm);
                    var assembly = Assembly.Load(code);//.LoadFromStream(asm);
                    Type newType = assembly.ExportedTypes.First(t => t.Name == Path.GetFileNameWithoutExtension(filepath.Name));
                    //Type newType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).First(t => t.Name == Path.GetFileNameWithoutExtension(filepath.Name));

                    //assemblyLoadContext.Unload();

                    Type originalType = componentTypes.FirstOrDefault(t => t.FullName == newType.FullName);
                    if (originalType != null)
                    {

                        LiveComponentContext context = null;
                        liveContexts.TryGetValue(originalType, out context);
                        if (context == null)
                        {
                            context = new LiveComponentContext()
                            {
                                OriginalMethod = originalType.GetMethod("BuildRenderTree", BindingFlags.NonPublic | BindingFlags.Instance),
                                NewType = newType,
                                Replacer = typeof(Blazor).GetMethod(nameof(ReplaceBuildRenderTree))//.MakeGenericMethod(originalType)
                            };
                            liveContexts[originalType] = context;
                        }
                        else
                        {
                            context.OriginalMethod ??= originalType.GetMethod("BuildRenderTree", BindingFlags.NonPublic | BindingFlags.Instance);
                            context.Replacer ??= typeof(Blazor).GetMethod(nameof(ReplaceBuildRenderTree));//.MakeGenericMethod(originalType)
                            context.NewType = newType;
                        }
                        currentType = originalType;
                        harmony.Patch(context.OriginalMethod, transpiler: new HarmonyMethod(context.Replacer));
                        context.Components.ForEach(c =>
                        {
                            Action rerender = () => stateHasChanged.Invoke(c, new object[] { });
                            invokeAsync.Invoke(c, new object[] { rerender });

                        });
                    }
                }
            });

            watcher.Start();
        }
    }
}
