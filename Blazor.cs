using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Reflection.Emit;
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

        public static bool Prefix(ComponentBase __instance)
        {
            var type = __instance.GetType();
            if (!liveContexts.ContainsKey(type))
            {
                liveContexts[type] = new LiveComponentContext();
            }
            var context = liveContexts[type];
            if (!context.Components.Contains(__instance))
            {
                context.Components.Add(__instance);
            }
            //if (context.NewType != null)
            //{
            //    Type newCompiledType = context.NewType;
            //    var newRenderTree = newCompiledType.GetMethod("BuildRenderTree", BindingFlags.NonPublic | BindingFlags.Instance);
            //    newRenderTree.Invoke(__instance, new object[] { null });
            //    return false;
            //}
            return true;
        }

        static Type currentType;
        public static IEnumerable<CodeInstruction> ReplaceBuildRenderTree/*<TComponent>*/(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var context = liveContexts[currentType];//[typeof(TComponent)];
            Type newCompiledType = context.NewType;
            var newRenderTree = newCompiledType.GetMethod("BuildRenderTree", BindingFlags.NonPublic | BindingFlags.Instance);

            var newInstructions = newRenderTree.GetInstructions();
            var labelledInstructions = newInstructions.Where(l => l.Operand is Instruction);
            Dictionary<Instruction, Label> instructionLabels = new Dictionary<Instruction, Label>();
            foreach(var instruction in labelledInstructions)
            {
                instructionLabels[instruction.Operand as Instruction] = generator.DefineLabel();
            }
            foreach(var instruction in newInstructions)
            {
                Label label;
                if (instructionLabels.TryGetValue(instruction, out label))
                {
                    generator.MarkLabel(label);
                }
                switch (instruction.OpCode.OperandType)
                {
                    default:
                        switch (instruction.Operand)
                        {
                            case bool i:
                                generator.Emit(instruction.OpCode, i == false ? 0 : 1);
                                break;
                            case byte i:
                                generator.Emit(instruction.OpCode, i);
                                break;
                            case sbyte i:
                                //STRANGE: pushing an int8 onto stack throws object reference exception
                                //so we change the instruction to push int32
                                if (instruction.OpCode == OpCodes.Ldc_I4_S) 
                                {
                                    generator.Emit(OpCodes.Ldc_I4, (int)i);
                                }
                                else
                                {
                                    generator.Emit(instruction.OpCode, i);
                                }
                                break;
                            case int i:
                                generator.Emit(instruction.OpCode, i);
                                break;
                            case uint i:
                                generator.Emit(instruction.OpCode, i);
                                break;
                            case long i:
                                generator.Emit(instruction.OpCode, i);
                                break;
                            case float i:
                                generator.Emit(instruction.OpCode, i);
                                break;
                            case double i:
                                generator.Emit(instruction.OpCode, i);
                                break;
                            case string i:
                                generator.Emit(instruction.OpCode, i);
                                break;
                            case Type i:
                                generator.Emit(instruction.OpCode, i);
                                break;
                            case MethodInfo i:
                                generator.Emit(instruction.OpCode, i);
                                break;
                            default:
                                if (instruction.Operand  != null)
                                {
                                    throw new Exception($"UnImplemented Instruction {instruction.OpCode} with operand {instruction.Operand}");
                                }
                                generator.Emit(instruction.OpCode);
                                break;
                        }
                        break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.InlineBrTarget:
                        Label branchLabel = instructionLabels[instruction.Operand as Instruction];
                        generator.Emit(instruction.OpCode, branchLabel);
                        break;
                }
            }

            //var newCodes = PatchProcessor.ReadMethodBody(newRenderTree);
            //foreach(var code in newCodes)
            //{
            //    switch (code.Key.OperandType)
            //    {
            //        default:
            //            switch (code.Value)
            //            {
            //                case bool i:
            //                    generator.Emit(code.Key, i == false ? 0 : 1);
            //                    break;
            //                case byte i:
            //                    generator.Emit(code.Key, i);
            //                    break;
            //                case sbyte i:
            //                    //generator.Emit(code.Key, i);
            //                    break;
            //                case int i:
            //                    generator.Emit(code.Key, i);
            //                    break;
            //                case uint i:
            //                    generator.Emit(code.Key, i);
            //                    break;
            //                case long i:
            //                    generator.Emit(code.Key, i);
            //                    break;
            //                case float i:
            //                    generator.Emit(code.Key, i);
            //                    break;
            //                case double i:
            //                    generator.Emit(code.Key, i);
            //                    break;
            //                case string i:
            //                    generator.Emit(code.Key, i);
            //                    break;
            //                case Type i:
            //                    generator.Emit(code.Key, i);
            //                    break;
            //                case MethodInfo i:
            //                    generator.Emit(code.Key, i);
            //                    break;
            //                default:
            //                    if (code.Value != null)
            //                    {

            //                    }
            //                    generator.Emit(code.Key);
            //                    break;
            //            }
            //            break;
            //        case OperandType.ShortInlineBrTarget:
            //        case OperandType.InlineBrTarget:
            //            break;
            //    }
            //}


            //return newCodes.Select(c => new CodeInstruction(c.Key, c.Value)).ToArray();
            //var newInstructions = newRenderTree.GetInstructions();
            //var newCodeInstructions = newInstructions.Select(i =>
            //{
            //    //var operand = i.Operand;
            //    //if (operand is Instruction ins)
            //    //{
            //    //    operand = new CodeInstruction(ins.OpCode, ins.Operand);
            //    //}
            //    return new CodeInstruction(i.OpCode, operand);
            //}).ToArray();
            //return newCodeInstructions;
            return new CodeInstruction[] { new CodeInstruction(OpCodes.Ret) };
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

        static Harmony harmony;
        static ObservableFileSystemWatcher watcher;
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

            harmony = new Harmony("com.liveblazor.livingthing");
            var compiler = new Compiler();

            var invokeAsync = typeof(ComponentBase).GetMethod("InvokeAsync", bindingAttr:BindingFlags.NonPublic | BindingFlags.Instance, types:new Type[] { typeof(Action) }, binder:null, modifiers:null);
            var stateHasChanged = typeof(ComponentBase).GetMethod("StateHasChanged", BindingFlags.NonPublic | BindingFlags.Instance);

            //find all components in all assemblies
            var componentTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).Where(t => !t.IsAbstract && !t.ContainsGenericParameters && t.MemberType == MemberTypes.TypeInfo && typeof(ComponentBase).IsAssignableFrom(t)).ToArray();
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

            watcher = new ObservableFileSystemWatcher(c =>
            {
                c.Path = watchPath;
                c.IncludeSubdirectories = true;
                c.Filter = "*.razor";
                c.NotifyFilter = NotifyFilters.Attributes |
                NotifyFilters.CreationTime |
                NotifyFilters.FileName |
                NotifyFilters.LastAccess |
                NotifyFilters.LastWrite |
                NotifyFilters.Size |
                NotifyFilters.Security;
                //c.Filters.Add("*.razor");
            });

            var changes = watcher.Changed.Throttle(TimeSpan.FromSeconds(.5));

            string dotnetPath = (await "where dotnet".CLI()).StdOut.Trim();
            string dotnetVersion = (await "dotnet --version".CLI()).StdOut.Trim();
            var dotnetFolder = Path.GetDirectoryName(dotnetPath) + "\\";

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
                List<string> sourceCodes = new List<string>() { file };
                var csFile = Path.ChangeExtension(filepath.FullPath, ".razor.cs");
                if (File.Exists(csFile))
                {
                    var csFileContent = File.ReadAllText(csFile);
                    sourceCodes.Add(csFileContent);
                }


                var code = compiler.Compile(sourceCodes.ToArray());

                using (var asm = new MemoryStream(code))
                {
                    var assemblyLoadContext = new UnloadableAssemblyLoadContext();

                    var assembly = assemblyLoadContext.LoadFromStream(asm);
                    // var assembly = Assembly.Load(code);//.LoadFromStream(asm);
                    Type newType = assembly.ExportedTypes.First(t => t.Name == Path.GetFileNameWithoutExtension(filepath.Name));
                    //Type newType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).First(t => t.Name == Path.GetFileNameWithoutExtension(filepath.Name));

                    assemblyLoadContext.Unload();

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
                        try
                        {
                            harmony.Patch(context.OriginalMethod, transpiler: new HarmonyMethod(context.Replacer));
                        }catch(Exception e)
                        {
                            Console.WriteLine(e.Message);
                            Console.WriteLine(e.StackTrace);
                            return;
                        }
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
