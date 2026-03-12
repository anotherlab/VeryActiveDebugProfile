//using Microsoft.VisualStudio.OLE.Interop;
using CommunityToolkit.Mvvm.Messaging;
using EnvDTE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using VeryActiveDebugProfile.Models;

namespace VeryActiveDebugProfile.Services;

public class VsProjectService
{
    [DllImport("ole32.dll")]
    static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable prot);

    [DllImport("ole32.dll")]
    static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);

    public void SendMessage(string message)
    {
        WeakReferenceMessenger.Default.Send(new StatusChangedMessage(message));
    }

    public List<string> GetMauiProjectsByInstance(VsInstance vsInstance)
    {
        var result = new List<string>();

        foreach (var project in vsInstance.Projects)
        {
            if (project.IsMaui)
            {
                result.Add(project.Path);
            }
        }

        return result;
    }
    public List<string> GetMauiProjectsByInstances(List<VsInstance> vsInstances)
    {
        var result = new List<string>();

        foreach (var vsInstance in vsInstances)
        {
            result.AddRange(GetMauiProjectsByInstance(vsInstance));
        }

        return result;
    }

    public List<VsInstance> GetVsInstances()
    {
        // Implementation to get VS instances
        var results = new List<VsInstance>();
        var vsFound = 0;
        SendMessage($"Scanning ROT...");

        try
        {
            GetRunningObjectTable(0, out IRunningObjectTable rot);
            CreateBindCtx(0, out IBindCtx ctx);

            rot.EnumRunning(out IEnumMoniker enumMoniker);
            IMoniker[] monikers = new IMoniker[1];

            while (enumMoniker.Next(1, monikers, IntPtr.Zero) == 0)
            {
                monikers[0].GetDisplayName(ctx, null, out string displayName);

                if (displayName.IndexOf("VisualStudio.DTE") >= 0)
                {
                    try
                    {
                        rot.GetObject(monikers[0], out object obj);
                        dynamic dte = obj;

                        string solution = dte.Solution.FullName;
                        if (string.IsNullOrEmpty(solution))
                            continue; // skip if no solution open

                        var vsInstance = new VsInstance
                        {
                            Version = dte.Version,
                            SolutionPath = solution,
                            Projects = new List<VsProject>()
                        };

                        // Get process ID safely via main window handle
                        int processId = 0;
                        try
                        {
                            int hwnd = dte.MainWindow.HWnd;
                            var processes = System.Diagnostics.Process.GetProcessesByName("devenv");
                            foreach (var p in processes)
                            {
                                if (p.MainWindowHandle.ToInt32() == hwnd)
                                {
                                    processId = p.Id;
                                    break;
                                }
                            }
                        }
                        catch
                        {
                            processId = 0;
                        }
                        vsInstance.ProcessId = processId;

                        SendMessage($"Enumerating projects...");

                        // Enumerate all loaded projects including nested solution folders
                        EnumerateProjects(dte.Solution.Projects, vsInstance.Projects);

                        vsFound++;

                        // Only include instances that have at least one MAUI project (optional filter)
                        if (vsInstance.Projects.Count > 0)
                            results.Add(vsInstance);

                        //if (VsInstanceHasMauiProjects(vsInstance))
                        //    results.Add(vsInstance);
                    }
                    catch
                    {
                        // ignore per-instance COM failures
                    }
                }
            }

            SendMessage($"Scanned ROT: Found {vsFound} Visual Studio instance(s).");

            // Output JSON
            //string json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
            //Console.WriteLine(json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error enumerating VS instances: {ex.Message}");
        }


        return results;
    }

    // Recursive project enumerator to handle nested solution folders


    static bool VsInstanceHasMauiProjects(VsInstance instance)
    {
        foreach (var proj in instance.Projects)
        {
            if (proj.IsMaui)
                return true;
        }
        return false;
    }

    static bool IsMauiProject(Project project)
    {
        var filePath = project.FullName;

        var doc = XDocument.Load(filePath);

        var useMaui = doc
            .Descendants("UseMaui")
            .FirstOrDefault();

        return useMaui != null &&
               bool.TryParse(useMaui.Value, out var result) &&
               result;
    }

    static void EnumerateProjects(Projects projects, List<VsProject> resultList)
    {
        foreach (Project proj in projects)
        {
            if (proj.Kind == EnvDTE80.ProjectKinds.vsProjectKindSolutionFolder)
            {
                // Recurse into solution folder
                EnumerateProjects(proj.ProjectItems, resultList);
            }
            else if (!string.IsNullOrEmpty(proj.FullName) &&
                     proj.FullName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                if (IsMauiProject(proj))
                {
                    resultList.Add(new VsProject
                    {
                        Name = proj.Name,
                        Path = proj.FullName,
                        IsMaui = true
                    });
                }
            }
        }
    }

    // Overload for ProjectItems in solution folders
    static void EnumerateProjects(ProjectItems items, List<VsProject> resultList)
    {
        foreach (ProjectItem item in items)
        {
            Project subProj = item.SubProject;
            if (subProj != null)
            {
                if (subProj.Kind == EnvDTE80.ProjectKinds.vsProjectKindSolutionFolder)
                {
                    EnumerateProjects(subProj.ProjectItems, resultList);
                }
                else if (!string.IsNullOrEmpty(subProj.FullName) &&
                         subProj.FullName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                {
                    resultList.Add(new VsProject
                    {
                        Name = subProj.Name,
                        Path = subProj.FullName
                    });
                }
            }
        }
    }


    public static int UpdateProjectFile(string csprojFullPath, string activeDebugProfile)
    {
        if (string.IsNullOrWhiteSpace(csprojFullPath))
            throw new ArgumentException("Project path cannot be null or empty.", nameof(csprojFullPath));

        if (!File.Exists(csprojFullPath))
            throw new FileNotFoundException("Project file not found.", csprojFullPath);

        if (string.IsNullOrWhiteSpace(activeDebugProfile))
            throw new ArgumentException("ActiveDebugProfile cannot be null or empty.", nameof(activeDebugProfile));

        var userFilePath = csprojFullPath + ".user";

        XDocument doc;
        XNamespace ns;

        if (File.Exists(userFilePath))
        {
            doc = XDocument.Load(userFilePath);
            ns = doc.Root?.Name.Namespace ?? "http://schemas.microsoft.com/developer/msbuild/2003";
        }
        else
        {
            // Create new .csproj.user file
            ns = "http://schemas.microsoft.com/developer/msbuild/2003";

            doc = new XDocument(
                new XElement(ns + "Project",
                    new XAttribute("ToolsVersion", "Current"),
                    new XElement(ns + "PropertyGroup")
                )
            );
        }

        var root = doc.Root ?? throw new InvalidOperationException("Invalid .user file structure.");

        var propertyGroup = root.Elements(ns + "PropertyGroup").FirstOrDefault();
        if (propertyGroup == null)
        {
            propertyGroup = new XElement(ns + "PropertyGroup");
            root.Add(propertyGroup);
        }

        var activeDebugProfileElement = propertyGroup
            .Elements(ns + "ActiveDebugProfile")
            .FirstOrDefault();

        if (activeDebugProfileElement == null)
        {
            activeDebugProfileElement = new XElement(ns + "ActiveDebugProfile");
            propertyGroup.Add(activeDebugProfileElement);
        }

        if (activeDebugProfileElement.Value.Contains(activeDebugProfile))
            return 0;

        activeDebugProfileElement.Value = activeDebugProfile;

        doc.Save(userFilePath);

        return 1;   
    }

}
