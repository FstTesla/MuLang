using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.IO;

namespace MuLang.VisualStudio;

internal static class MuLangSolutionSourceService
{
    public static IReadOnlyCollection<string>? GetDocumentUris(
        IServiceProvider serviceProvider
    )
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (
            serviceProvider.GetService(typeof(SVsSolution)) is
            not IVsSolution solution
        )
        {
            return null;
        }

        ErrorHandler.ThrowOnFailure(
            solution.GetProperty(
                (int)__VSPROPID.VSPROPID_IsSolutionOpen,
                out object isSolutionOpen
            )
        );

        if (isSolutionOpen is not true)
        {
            return null;
        }

        ISet<string> uris = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase
        );
        Guid projectType = Guid.Empty;
        ErrorHandler.ThrowOnFailure(
            solution.GetProjectEnum(
                (uint)__VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION,
                ref projectType,
                out IEnumHierarchies projects
            )
        );
        IVsHierarchy[] hierarchies = new IVsHierarchy[1];

        while (true)
        {
            ErrorHandler.ThrowOnFailure(
                projects.Next(1, hierarchies, out uint fetched)
            );

            if (fetched == 0)
            {
                break;
            }

            AddDocuments(hierarchies[0], VSConstants.VSITEMID_ROOT, uris);
        }

        return
        [
            .. uris.OrderBy(
                static uri => uri,
                StringComparer.OrdinalIgnoreCase
            ),
        ];
    }

    private static void AddDocuments(
        IVsHierarchy hierarchy,
        uint item,
        ISet<string> uris
    )
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (
            ErrorHandler.Succeeded(
                hierarchy.GetProperty(
                    item,
                    (int)__VSHPROPID.VSHPROPID_IsNonMemberItem,
                    out object isNonMember
                )
            ) &&
            isNonMember is true
        )
        {
            return;
        }

        if (
            ErrorHandler.Succeeded(
                hierarchy.GetCanonicalName(item, out string path)
            ) &&
            !string.IsNullOrEmpty(path) &&
            Path.IsPathRooted(path) &&
            (
                string.Equals(
                    Path.GetExtension(path),
                    ".mu",
                    StringComparison.OrdinalIgnoreCase
                ) ||
                string.Equals(
                    Path.GetExtension(path),
                    ".mulang",
                    StringComparison.OrdinalIgnoreCase
                )
            )
        )
        {
            uris.Add(new Uri(Path.GetFullPath(path)).AbsoluteUri);
        }

        uint child = GetItemId(
            hierarchy,
            item,
            __VSHPROPID.VSHPROPID_FirstChild
        );

        while (child != VSConstants.VSITEMID_NIL)
        {
            AddDocuments(hierarchy, child, uris);
            child = GetItemId(
                hierarchy,
                child,
                __VSHPROPID.VSHPROPID_NextSibling
            );
        }
    }

    private static uint GetItemId(
        IVsHierarchy hierarchy,
        uint item,
        __VSHPROPID property
    )
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        int result = hierarchy.GetProperty(
            item,
            (int)property,
            out object value
        );

        if (result == VSConstants.E_NOTIMPL)
        {
            return VSConstants.VSITEMID_NIL;
        }

        ErrorHandler.ThrowOnFailure(result);

        return value switch
        {
            int id => unchecked((uint)id),
            uint id => id,
            IntPtr id => unchecked((uint)id.ToInt64()),
            null => VSConstants.VSITEMID_NIL,
            _ => throw new InvalidOperationException(
                $"Unexpected hierarchy item ID '{value}'."
            ),
        };
    }
}
