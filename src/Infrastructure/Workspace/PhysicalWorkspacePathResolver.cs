using System.Security;
using AiGameStudioWorkstation.Application.Workspace;
using AiGameStudioWorkstation.Core.Domain.Workspace;

namespace AiGameStudioWorkstation.Infrastructure.Workspace;

public sealed class PhysicalWorkspacePathResolver : IWorkspacePathResolver
{
    public WorkspacePathResolutionResult Resolve(WorkspacePathResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var operationRequest = request.OperationRequest;
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            return Failure(
                request,
                WorkspacePathResolutionStatus.Indeterminate,
                WorkspacePathResolutionFailure.UnsupportedFilesystemConfiguration);
        }

        string physicalRoot;
        try
        {
            if (!Path.IsPathFullyQualified(request.ConfiguredPhysicalRoot))
            {
                return Failure(
                    request,
                    WorkspacePathResolutionStatus.Failed,
                    WorkspacePathResolutionFailure.InvalidPhysicalRoot);
            }

            physicalRoot = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(request.ConfiguredPhysicalRoot));

            if (IsUnsupportedNetworkOrDeviceRoot(physicalRoot))
            {
                return Failure(
                    request,
                    WorkspacePathResolutionStatus.Indeterminate,
                    WorkspacePathResolutionFailure.UnsupportedPhysicalRoot);
            }
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            return Failure(
                request,
                WorkspacePathResolutionStatus.Failed,
                WorkspacePathResolutionFailure.InvalidPhysicalPath);
        }

        HashSet<string>? linuxMountPoints = null;
        if (OperatingSystem.IsLinux())
        {
            try
            {
                linuxMountPoints = ReadLinuxMountPoints();
            }
            catch (Exception exception) when (IsFilesystemException(exception) || exception is InvalidDataException)
            {
                return Failure(
                    request,
                    WorkspacePathResolutionStatus.Indeterminate,
                    WorkspacePathResolutionFailure.UnsupportedFilesystemConfiguration);
            }
        }

        var rootCheck = VerifyExistingDirectoryChain(physicalRoot);
        if (rootCheck.Status is not null)
        {
            return Failure(request, rootCheck.Status.Value, rootCheck.Failure!.Value);
        }

        var resourceSegments = GetSegments(operationRequest.ResourceId.NormalizedValue);
        var matchingPermittedRoot = request.Workspace.PermittedRoots
            .Select(root => GetSegments(root.NormalizedValue))
            .Where(rootSegments => IsSegmentPrefix(rootSegments, resourceSegments))
            .OrderByDescending(rootSegments => rootSegments.Length)
            .FirstOrDefault();

        string candidatePath;
        try
        {
            candidatePath = Path.GetFullPath(CombineSegments(physicalRoot, resourceSegments));
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            return Failure(
                request,
                WorkspacePathResolutionStatus.Failed,
                WorkspacePathResolutionFailure.InvalidPhysicalPath);
        }

        if (!IsPathWithin(physicalRoot, candidatePath))
        {
            return Outside(request, candidatePath);
        }

        if (matchingPermittedRoot is null)
        {
            return Outside(request, candidatePath);
        }

        string permittedRootPath;
        try
        {
            permittedRootPath = Path.GetFullPath(CombineSegments(physicalRoot, matchingPermittedRoot));
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            return Failure(
                request,
                WorkspacePathResolutionStatus.Failed,
                WorkspacePathResolutionFailure.InvalidPhysicalPath,
                candidatePath);
        }

        if (!IsPathWithin(permittedRootPath, candidatePath))
        {
            return Outside(request, candidatePath);
        }

        var targetCheck = VerifyResourcePath(
            physicalRoot,
            resourceSegments,
            operationRequest.Operation,
            linuxMountPoints);
        if (targetCheck.Status is not null)
        {
            return Failure(
                request,
                targetCheck.Status.Value,
                targetCheck.Failure!.Value,
                candidatePath);
        }

        var pathIsExisting = targetCheck.TargetKind is WorkspacePathTargetKind.ExistingFile
            or WorkspacePathTargetKind.ExistingDirectory;
        return new WorkspacePathResolutionResult(
            request.Workspace.Id,
            request.Workspace.RootId,
            operationRequest.ResourceId,
            operationRequest.Operation,
            WorkspacePathResolutionStatus.ResolvedWithinPermittedRoot,
            WorkspaceResourceBoundary.WithinPermittedWorkspace,
            candidatePath,
            pathIsExisting ? candidatePath : null,
            targetCheck.VerifiedAncestorPath!,
            targetCheck.TargetKind,
            failure: null);
    }

    private static (WorkspacePathResolutionStatus? Status, WorkspacePathResolutionFailure? Failure)
        VerifyExistingDirectoryChain(string path)
    {
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(root))
        {
            return (WorkspacePathResolutionStatus.Failed, WorkspacePathResolutionFailure.InvalidPhysicalRoot);
        }

        var currentPath = Path.TrimEndingDirectorySeparator(root);
        if (currentPath.Length == 0)
        {
            currentPath = root;
        }

        var rootAttributes = TryGetAttributes(currentPath, out var readStatus);
        if (readStatus is not null)
        {
            return readStatus.Value;
        }

        if (rootAttributes is null || !IsDirectory(rootAttributes.Value))
        {
            return (WorkspacePathResolutionStatus.Failed, WorkspacePathResolutionFailure.InvalidPhysicalRoot);
        }

        if (IsReparsePoint(rootAttributes.Value))
        {
            return (WorkspacePathResolutionStatus.Rejected, WorkspacePathResolutionFailure.SymbolicLinkOrReparsePoint);
        }

        var relativeSegments = GetPhysicalSegments(path, root);
        foreach (var segment in relativeSegments)
        {
            currentPath = Path.Combine(currentPath, segment);
            var attributes = TryGetAttributes(currentPath, out readStatus);
            if (readStatus is not null)
            {
                return readStatus.Value;
            }

            if (attributes is null || !IsDirectory(attributes.Value))
            {
                return (WorkspacePathResolutionStatus.Failed, WorkspacePathResolutionFailure.InvalidPhysicalRoot);
            }

            if (IsReparsePoint(attributes.Value))
            {
                return (WorkspacePathResolutionStatus.Rejected, WorkspacePathResolutionFailure.SymbolicLinkOrReparsePoint);
            }
        }

        return (null, null);
    }

    private static TargetCheck VerifyResourcePath(
        string physicalRoot,
        string[] segments,
        WorkspaceOperation operation,
        IReadOnlySet<string>? linuxMountPoints)
    {
        var currentPath = physicalRoot;
        var mostRecentExistingDirectory = physicalRoot;
        var missingPathStarted = false;

        for (var index = 0; index < segments.Length; index++)
        {
            currentPath = Path.Combine(currentPath, segments[index]);
            var attributes = TryGetAttributes(currentPath, out var readStatus);
            if (readStatus is not null)
            {
                return TargetCheck.Failed(readStatus.Value.Status, readStatus.Value.Failure);
            }

            if (attributes is null)
            {
                missingPathStarted = true;
                var isLastSegment = index == segments.Length - 1;
                if (operation == WorkspaceOperation.CreateDirectory)
                {
                    continue;
                }

                if (operation == WorkspaceOperation.Write && isLastSegment)
                {
                    return TargetCheck.Prospective(
                        WorkspacePathTargetKind.ProspectiveFile,
                        mostRecentExistingDirectory);
                }

                return TargetCheck.Failed(
                    WorkspacePathResolutionStatus.NotFound,
                    isLastSegment
                        ? WorkspacePathResolutionFailure.MissingTarget
                        : WorkspacePathResolutionFailure.MissingAncestor);
            }

            if (missingPathStarted)
            {
                return TargetCheck.Failed(
                    WorkspacePathResolutionStatus.Indeterminate,
                    WorkspacePathResolutionFailure.FilesystemAccessError);
            }

            if (linuxMountPoints?.Contains(currentPath) == true &&
                !string.Equals(currentPath, physicalRoot, StringComparison.Ordinal))
            {
                return TargetCheck.Failed(
                    WorkspacePathResolutionStatus.Rejected,
                    WorkspacePathResolutionFailure.UnsupportedFilesystemConfiguration);
            }

            if (IsReparsePoint(attributes.Value))
            {
                return TargetCheck.Failed(
                    WorkspacePathResolutionStatus.Rejected,
                    WorkspacePathResolutionFailure.SymbolicLinkOrReparsePoint);
            }

            var isLast = index == segments.Length - 1;
            var isDirectory = IsDirectory(attributes.Value);
            if (!isLast && !isDirectory)
            {
                return TargetCheck.Failed(
                    WorkspacePathResolutionStatus.Rejected,
                    WorkspacePathResolutionFailure.NonDirectoryAncestor);
            }

            if (!isLast)
            {
                mostRecentExistingDirectory = currentPath;
                continue;
            }

            if (operation == WorkspaceOperation.Read && isDirectory ||
                operation == WorkspaceOperation.List && !isDirectory ||
                operation == WorkspaceOperation.Write && isDirectory)
            {
                return TargetCheck.Failed(
                    WorkspacePathResolutionStatus.Rejected,
                    WorkspacePathResolutionFailure.WrongTargetKind);
            }

            if (operation == WorkspaceOperation.CreateDirectory && !isDirectory)
            {
                return TargetCheck.Failed(
                    WorkspacePathResolutionStatus.Rejected,
                    WorkspacePathResolutionFailure.WrongTargetKind);
            }

            return TargetCheck.Existing(
                isDirectory
                    ? WorkspacePathTargetKind.ExistingDirectory
                    : WorkspacePathTargetKind.ExistingFile,
                isDirectory ? currentPath : mostRecentExistingDirectory);
        }

        if (operation == WorkspaceOperation.CreateDirectory && missingPathStarted)
        {
            return TargetCheck.Prospective(
                WorkspacePathTargetKind.ProspectiveDirectory,
                mostRecentExistingDirectory);
        }

        return TargetCheck.Failed(
            WorkspacePathResolutionStatus.Indeterminate,
            WorkspacePathResolutionFailure.FilesystemAccessError);
    }

    private static FileAttributes? TryGetAttributes(
        string path,
        out (WorkspacePathResolutionStatus Status, WorkspacePathResolutionFailure Failure)? readStatus)
    {
        try
        {
            readStatus = null;
            return File.GetAttributes(path);
        }
        catch (FileNotFoundException)
        {
            readStatus = null;
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            readStatus = null;
            return null;
        }
        catch (Exception exception) when (IsFilesystemException(exception))
        {
            readStatus = (WorkspacePathResolutionStatus.Indeterminate, WorkspacePathResolutionFailure.FilesystemAccessError);
            return null;
        }
    }

    private static bool IsFilesystemException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException;

    private static bool IsPathException(Exception exception) =>
        IsFilesystemException(exception) || exception is ArgumentException;

    private static bool IsUnsupportedNetworkOrDeviceRoot(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var pathRoot = Path.GetPathRoot(path);
        return pathRoot is not null &&
            (pathRoot.StartsWith(@"\\", StringComparison.Ordinal) ||
             pathRoot.StartsWith(@"\\.\", StringComparison.Ordinal) ||
             pathRoot.StartsWith(@"\\?\", StringComparison.Ordinal));
    }

    private static HashSet<string> ReadLinuxMountPoints()
    {
        var mountPoints = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines("/proc/self/mountinfo"))
        {
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 6)
            {
                throw new InvalidDataException("The Linux mount table has an unexpected format.");
            }

            mountPoints.Add(Path.GetFullPath(UnescapeMountPath(fields[4])));
        }

        if (mountPoints.Count == 0)
        {
            throw new InvalidDataException("The Linux mount table contains no mount points.");
        }

        return mountPoints;
    }

    private static string UnescapeMountPath(string value)
    {
        var result = new System.Text.StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '\\' &&
                index + 3 < value.Length &&
                IsOctalDigit(value[index + 1]) &&
                IsOctalDigit(value[index + 2]) &&
                IsOctalDigit(value[index + 3]))
            {
                var decoded = (char)(
                    (value[index + 1] - '0') * 64 +
                    (value[index + 2] - '0') * 8 +
                    value[index + 3] - '0');
                result.Append(decoded);
                index += 3;
            }
            else
            {
                result.Append(value[index]);
            }
        }

        return result.ToString();
    }

    private static bool IsOctalDigit(char value) => value is >= '0' and <= '7';

    private static string[] GetSegments(string normalizedRelativePath) =>
        normalizedRelativePath.Split('/');

    private static string[] GetPhysicalSegments(string absolutePath, string root)
    {
        var relativePath = absolutePath[root.Length..];
        return relativePath
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
    }

    private static string CombineSegments(string root, IEnumerable<string> segments) =>
        segments.Aggregate(root, Path.Combine);

    private static bool IsSegmentPrefix(IReadOnlyList<string> prefix, IReadOnlyList<string> value)
    {
        if (prefix.Count > value.Count)
        {
            return false;
        }

        for (var index = 0; index < prefix.Count; index++)
        {
            if (!string.Equals(prefix[index], value[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPathWithin(string root, string candidate)
    {
        var relativePath = Path.GetRelativePath(root, candidate);
        return !Path.IsPathRooted(relativePath) &&
            relativePath != ".." &&
            !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static bool IsDirectory(FileAttributes attributes) =>
        attributes.HasFlag(FileAttributes.Directory);

    private static bool IsReparsePoint(FileAttributes attributes) =>
        attributes.HasFlag(FileAttributes.ReparsePoint);

    private static WorkspacePathResolutionResult Outside(
        WorkspacePathResolutionRequest request,
        string candidatePath) =>
        new(
            request.Workspace.Id,
            request.Workspace.RootId,
            request.OperationRequest.ResourceId,
            request.OperationRequest.Operation,
            WorkspacePathResolutionStatus.OutsidePermittedRoot,
            WorkspaceResourceBoundary.OutsidePermittedWorkspace,
            candidatePath,
            verifiedTargetPath: null,
            verifiedAncestorPath: null,
            targetKind: null,
            WorkspacePathResolutionFailure.ResourceOutsidePermittedRoots);

    private static WorkspacePathResolutionResult Failure(
        WorkspacePathResolutionRequest request,
        WorkspacePathResolutionStatus status,
        WorkspacePathResolutionFailure failure,
        string? candidatePath = null) =>
        new(
            request.Workspace.Id,
            request.Workspace.RootId,
            request.OperationRequest.ResourceId,
            request.OperationRequest.Operation,
            status,
            WorkspaceResourceBoundary.Unknown,
            candidatePath,
            verifiedTargetPath: null,
            verifiedAncestorPath: null,
            targetKind: null,
            failure);

    private sealed record TargetCheck(
        WorkspacePathResolutionStatus? Status,
        WorkspacePathResolutionFailure? Failure,
        WorkspacePathTargetKind? TargetKind,
        string? VerifiedAncestorPath)
    {
        public static TargetCheck Failed(
            WorkspacePathResolutionStatus status,
            WorkspacePathResolutionFailure failure) =>
            new(status, failure, null, null);

        public static TargetCheck Existing(WorkspacePathTargetKind kind, string verifiedAncestorPath) =>
            new(null, null, kind, verifiedAncestorPath);

        public static TargetCheck Prospective(WorkspacePathTargetKind kind, string verifiedAncestorPath) =>
            new(null, null, kind, verifiedAncestorPath);
    }
}
