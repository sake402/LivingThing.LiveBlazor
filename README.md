# LivingThing.LiveBlazor
Update Blazor component in realtime without losing component state, without reloading

Usage
---
Add namespace

```
using LivingThing.LiveBlazor;
```

Initialize it
```
Blazor.Live()
```

You are done.

How it works under the hood
---
When you call Blazor.Live, the current Application domain is scanned for all assemby and types that extends ComponentBase. 
So If you have a component in a class library, you want to make sure the library is already loaded before calling Blazor.Live. You can force an assemby to load by simply doing
```
_ = typeof(Namespace.TypeInAssemby).Assemby
```
We then use [Harmony](https://github.com/pardeike/Harmony) project to patch the found types inserting prefix call into their ``BuildRenderTree`` function

This is so we can keep a track of every instance of a type created so far and can therefore automatically call their StateHasChanged when the razor file changes.

TODO: Remove component from the static collection when disposed to avoid memory hog.

We setup a FileWatcher on the project or solution directory so we know when a razor file is changed. 
When changed, we invoke dotnet to regenerate the .g.cs code for the razor file. The resulting cs file is then compiled against the current types in the application domain.
We load the compiled assembly and search for the System.Type of the razor file changed. We also identify the original System.Type that was initially compiled into the application.

We patch the original ``BuildRenderTree`` method replacing its IL code with the ILCode of the newly compiled ``BuildRenderTree`` method and call StateHasChanged on all component instances of this type.

What is not working yet
---
For some unknown reason, if after patching the ``BuildRenderTree`` one type, we also try to patch another ``BuildRenderTree`` entirely different from the first one, Harmony throws exception. This happens when you have editted A.razor and then try to edit B.razor.

If a new patch contains a branch instruction, exception. Converting the MethodInfo.GetILAsByteArray() to opcodes is not perfect yet.




