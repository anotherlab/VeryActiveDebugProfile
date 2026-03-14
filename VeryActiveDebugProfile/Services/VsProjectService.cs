using CommunityToolkit.Mvvm.Messaging;
using EnvDTE;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Xml.Linq;

namespace VeryActiveDebugProfile.Services;

public class VsProjectService
{
#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
    [DllImport("ole32.dll")]
    static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable prot);

    [DllImport("ole32.dll")]
    static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);
#pragma warning restore SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time

    private static void SendMessage(string message)
    {
        WeakReferenceMessenger.Default.Send(new StatusChangedMessage(message));
    }

    /// <summary>
    /// Retrieves a list of .NET MAUI project names available in the current Visual Studio instances.
    /// </summary>
    /// <returns>A list of strings containing the names of all detected .NET MAUI projects. The list is empty if no MAUI projects
    /// are found.</returns>
    public List<string> GetMauiProjects()
    {
        var vsInstances = GetVsInstances();
        var result = GetMauiProjectsByInstances(vsInstances);

        return result;
    }

    /// <summary>
    /// Retrieves the file paths of all MAUI projects contained within the specified Visual Studio instance.
    /// </summary>
    /// <param name="vsInstance">The Visual Studio instance from which to enumerate projects. Cannot be null.</param>
    /// <returns>A list of strings representing the file paths of all MAUI projects in the given instance. The list is empty if
    /// no MAUI projects are found.</returns>
    public static List<string> GetMauiProjectsByInstance(VsInstance vsInstance)
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

    /// <summary>
    /// Retrieves the list of MAUI project paths from the specified Visual Studio instances.
    /// </summary>
    /// <param name="vsInstances">A list of Visual Studio instances from which to collect MAUI project paths. Cannot be null.</param>
    /// <returns>A list of strings containing the paths of all MAUI projects found in the provided Visual Studio instances. The
    /// list will be empty if no projects are found.</returns>
    public static List<string> GetMauiProjectsByInstances(List<VsInstance> vsInstances)
    {
        var result = new List<string>();

        foreach (var vsInstance in vsInstances)
        {
            result.AddRange(GetMauiProjectsByInstance(vsInstance));
        }

        return result;
    }

    /// <summary>
    /// Retrieves a list of running Visual Studio instances that have an open solution and at least one loaded project.
    /// </summary>
    /// <remarks>Only Visual Studio instances with an open solution and at least one loaded project are
    /// included in the returned list. Instances without an open solution or without loaded projects are excluded. This
    /// method may skip instances if they cannot be accessed due to COM errors or if required information is
    /// unavailable.</remarks>
    /// <returns>A list of <see cref="VsInstance"/> objects representing the active Visual Studio instances with open solutions
    /// and loaded projects. The list is empty if no such instances are found.</returns>
    public List<VsInstance> GetVsInstances()
    {
        // Implementation to get VS instances
        var results = new List<VsInstance>();
        var vsFound = 0;

        try
        {
            // Get the ROT and enumerate running objects
            _ = GetRunningObjectTable(0, out IRunningObjectTable rot);
            _ = CreateBindCtx(0, out IBindCtx ctx);

            // Get the list of running objects from the ROT
            rot.EnumRunning(out IEnumMoniker enumMoniker);
            IMoniker[] monikers = new IMoniker[1];

            while (enumMoniker.Next(1, monikers, IntPtr.Zero) == 0)
            {
                // Get the display name of the running object
                monikers[0].GetDisplayName(ctx, null, out string displayName);

                // If we are Visual Studio, try to get the DTE object and extract solution and project information
                if (displayName.Contains("VisualStudio.DTE", StringComparison.CurrentCulture))
                {
                    try
                    {
                        rot.GetObject(monikers[0], out object obj);

                        dynamic dte = obj;

                        string solution = dte.Solution.FullName;

                        // Allow projects without a solution to be included, but skip instances
                        // that have no solution and no projects to avoid noise
                        if (string.IsNullOrEmpty(solution) && (dte.Solution.Projects.Count == 0))
                            continue; 

                        var vsInstance = new VsInstance
                        {
                            Version = dte.Version,
                            SolutionPath = solution,
                            Projects = []
                        };

                        if (string.IsNullOrEmpty(solution))
                            SendMessage($"Project without a solution found");
                        else
                            SendMessage($"Solution found: {solution}");

                        // Enumerate all loaded projects including nested solution folders
                        EnumerateProjects(dte.Solution.Projects, vsInstance.Projects);

                        vsFound++;

                        // Only include instances that have at least one MAUI project (optional filter)
                        if (vsInstance.Projects.Count > 0)
                            results.Add(vsInstance);
                    }
                    catch
                    {
                        // ignore per-instance COM failures
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error enumerating VS instances: {ex.Message}");
        }

        return results;
    }

    /// <summary>
    /// Determines whether the specified Visual Studio instance contains any MAUI projects.
    /// </summary>
    /// <param name="instance">The Visual Studio instance to inspect for MAUI projects. Cannot be null.</param>
    /// <returns>true if at least one project in the instance is a MAUI project; otherwise, false.</returns>
    static bool VsInstanceHasMauiProjects(VsInstance instance)
    {
        foreach (var proj in instance.Projects)
        {
            if (proj.IsMaui)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Determines whether the specified project is a .NET MAUI project based on its project file contents.
    /// </summary>
    /// <remarks>This method inspects the project's file for the presence of the <UseMaui> element to identify
    /// .NET MAUI projects. The project file must be accessible and well-formed XML.</remarks>
    /// <param name="project">The project to evaluate for .NET MAUI compatibility. Must not be null and should have a valid project file path.</param>
    /// <returns>true if the project file contains a <UseMaui> element set to true; otherwise, false.</returns>
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

    /// <summary>
    /// Recursively enumerates all C# projects within the specified projects collection and adds them to the provided result list.
    /// </summary>
    /// <param name="projects">The collection of projects to search for C# projects. May include solution folders and nested projects.</param>
    /// <param name="resultList">The list to which discovered C# projects are added. Must not be null.</param>
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

    /// <summary>
    /// Recursively enumerates all C# project files within the specified project items collection and adds them to the
    /// provided result list.
    /// </summary>
    /// <remarks>Only projects with a file name ending in ".csproj" are added to the result list. Solution
    /// folders are traversed recursively to locate nested projects.</remarks>
    /// <param name="items">The collection of project items to search for C# projects. May include solution folders and nested projects.</param>
    /// <param name="resultList">The list to which discovered C# projects are added. Must not be null.</param>
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


    /// <summary>
    /// Method to update the .csproj.user file with the specified ActiveDebugProfile. 
    /// If the .user file does not exist, it will be created. 
    /// If the ActiveDebugProfile already exists and matches the value, no changes will be made.
    /// </summary>
    /// <param name="csprojFullPath">The full path to the .csproj file.</param>
    /// <param name="activeDebugProfile">The ActiveDebugProfile value to set.</param>
    /// <returns>Returns 1 if the file was updated, 0 if no changes were made.</returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
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
