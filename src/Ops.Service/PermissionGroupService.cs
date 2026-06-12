using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Ocuda.Ops.Models.Abstract;
using Ocuda.Ops.Models.Definitions;
using Ocuda.Ops.Models.Entities;
using Ocuda.Ops.Models.Keys;
using Ocuda.Ops.Service.Abstract;
using Ocuda.Ops.Service.Filters;
using Ocuda.Ops.Service.Interfaces.Ops.Repositories;
using Ocuda.Ops.Service.Interfaces.Ops.Services;
using Ocuda.Utility.Abstract;
using Ocuda.Utility.Exceptions;
using Ocuda.Utility.Models;

namespace Ocuda.Ops.Service
{
    public class PermissionGroupService(ILogger<PermissionGroupService> logger,
        IHttpContextAccessor httpContextAccessor,
        IDateTimeProvider dateTimeProvider,
        IPermissionGroupApplicationRepository permissionGroupApplicationRepository,
        IPermissionGroupIncidentLocationRepository permissionGroupIncidentLocationRepository,
        IPermissionGroupPageContentRepository permissionGroupPageContentRepository,
        IPermissionGroupPodcastItemRepository permissionGroupPodcastItemRepository,
        IPermissionGroupProductManagerRepository permissionGroupProductManagerRepository,
        IPermissionGroupReplaceFilesRepository permissionGroupReplaceFilesRepository,
        IPermissionGroupReportingRepository permissionGroupReportingRepository,
        IPermissionGroupRepository permissionGroupRepository,
        IPermissionGroupSectionManagerRepository permissionGroupSectionManagerRepository)
            : BaseService<PermissionGroupService>(logger, httpContextAccessor),
            IPermissionGroupService
    {
        public async Task AddApplicationPermissionGroupAsync(string applicationPermission,
            int permissionGroupId)
        {
            var permission = ApplicationPermissionDefinitions.ApplicationPermissions
                .SingleOrDefault(_ => string.Equals(_.Id, applicationPermission,
                    StringComparison.OrdinalIgnoreCase))
                ?? throw new OcudaException("Invalid application permission.");

            await permissionGroupApplicationRepository.AddAsync(new PermissionGroupApplication
            {
                ApplicationPermission = applicationPermission,
                PermissionGroupId = permissionGroupId
            });
            await permissionGroupApplicationRepository.SaveAsync();
        }

        public async Task<PermissionGroup> AddAsync(PermissionGroup permissionGroup)
        {
            ArgumentNullException.ThrowIfNull(permissionGroup);

            permissionGroup.CreatedAt = dateTimeProvider.Now;
            permissionGroup.CreatedBy = GetCurrentUserId();
            permissionGroup.GroupName = permissionGroup.GroupName?.Trim();
            permissionGroup.PermissionGroupName = permissionGroup.PermissionGroupName?.Trim();

            await ValidateAsync(permissionGroup);

            await permissionGroupRepository.AddAsync(permissionGroup);
            await permissionGroupRepository.SaveAsync();

            return permissionGroup;
        }

        public async Task AddToPermissionGroupAsync<T>(int itemId, int permissionGroupId)
            where T : PermissionGroupMappingBase
        {
            if (GetAddPermissionMap().TryGetValue(typeof(T), out var delegateMethod))
            {
                await delegateMethod(itemId, permissionGroupId);
                _logger.LogInformation("User {CurrentUser} just added permission group id {PermissionGroupId} to {ItemType} id {ItemId}",
                    GetCurrentUserId(),
                    permissionGroupId,
                    typeof(T).Name,
                    itemId);
            }
            else
            {
                throw new OcudaException($"Unable to set permissions for {typeof(T)}");
            }
        }

        public async Task DeleteAsync(int permissionGroupId)
        {
            var assignedPermissions = await permissionGroupApplicationRepository
                .GetAssignedPermissions(permissionGroupId);

            if (assignedPermissions.Count > 0)
            {
                var ocudaException = new OcudaException();
                ocudaException.Data.Add("Assigned", assignedPermissions);
                throw ocudaException;
            }

            permissionGroupRepository.Remove(permissionGroupId);
            await permissionGroupRepository.SaveAsync();
        }

        public async Task<PermissionGroup> EditAsync(PermissionGroup permissionGroup)
        {
            ArgumentNullException.ThrowIfNull(permissionGroup);

            var currentPermissionGroup
                = await permissionGroupRepository.FindAsync(permissionGroup.Id);

            currentPermissionGroup.GroupName = permissionGroup.GroupName?.Trim();
            currentPermissionGroup.PermissionGroupName
                = permissionGroup.PermissionGroupName?.Trim();
            currentPermissionGroup.UpdatedAt = dateTimeProvider.Now;
            currentPermissionGroup.UpdatedBy = GetCurrentUserId();

            await ValidateAsync(currentPermissionGroup);

            permissionGroupRepository.Update(currentPermissionGroup);
            await permissionGroupRepository.SaveAsync();

            return currentPermissionGroup;
        }

        public async Task<(bool siteAdminRights, bool contentAdminRights)>
            GetAdminRightsAsync(IEnumerable<int> permissionGroupIds)
        {
            var hasSiteAdminRights = (
                await HasAPermissionAsync<PermissionGroupPageContent>(permissionGroupIds)
                || await HasAPermissionAsync<PermissionGroupPodcastItem>(permissionGroupIds)
                || await HasAPermissionAsync<PermissionGroupProductManager>(permissionGroupIds));

            if (!hasSiteAdminRights)
            {
                var emediaGroups = await GetApplicationPermissionGroupsAsync(ApplicationPermission
                    .EmediaManagement);

                hasSiteAdminRights = emediaGroups
                    .Select(_ => _.Id)
                    .Intersect(permissionGroupIds)
                    .Any();
            }

            var hasContentAdminRights = (
                await HasAPermissionAsync<PermissionGroupReplaceFiles>(permissionGroupIds)
                || await HasAPermissionAsync<PermissionGroupSectionManager>(permissionGroupIds));

            if (!hasContentAdminRights)
            {
                var ddGroups = await GetApplicationPermissionGroupsAsync(ApplicationPermission
                    .DigitalDisplayContentManagement);

                hasContentAdminRights = ddGroups
                    .Select(_ => _.Id)
                    .Intersect(permissionGroupIds)
                    .Any();
            }

            return (siteAdminRights: hasSiteAdminRights, contentAdminRights: hasContentAdminRights);
        }

        public async Task<ICollection<PermissionGroup>> GetAllAsync()
        {
            var permissions = await permissionGroupRepository.GetAllAsync();
            return [.. permissions.OrderBy(_ => _.PermissionGroupName)];
        }

        public async Task<int> GetApplicationPermissionGroupCountAsync(string permission)
        {
            return await permissionGroupApplicationRepository
                .GetApplicationPermissionGroupCountAsync(permission);
        }

        public async Task<ICollection<PermissionGroup>> GetApplicationPermissionGroupsAsync(
            string permission)
        {
            return await permissionGroupApplicationRepository
                .GetApplicationPermissionGroupsAsync(permission);
        }

        public async Task<ICollection<PermissionGroup>>
            GetGroupsAsync(IEnumerable<int> permissionGroupIds)
        {
            return await permissionGroupRepository.GetGroupsAsync(permissionGroupIds);
        }

        public async Task<IEnumerable<int>>
            GetItemIdAccessAsync<T>(IEnumerable<int> permissionGroupIds)
            where T : PermissionGroupMappingBase
        {
            if (GetItemAccessMap().TryGetValue(typeof(T), out var delegateMethod))
            {
                return await delegateMethod([.. permissionGroupIds]);
            }
            else
            {
                throw new OcudaException($"Unable to look up permissions for {typeof(T)}");
            }
        }

        public async Task<DataWithCount<ICollection<PermissionGroup>>>
            GetPaginatedListAsync(BaseFilter filter)
        {
            return await permissionGroupRepository.GetPaginatedListAsync(filter);
        }

        public async Task<ICollection<T>> GetPermissionsAsync<T>(int itemId)
            where T : PermissionGroupMappingBase
        {
            if (GetLookupPermissionMap().TryGetValue(typeof(T), out var delegateMethod))
            {
                return (await delegateMethod(itemId)) as ICollection<T>;
            }

            throw new OcudaException($"Unable to get permissions for {typeof(T)}");
        }

        public async Task<bool>
            HasAPermissionAsync<T>(IEnumerable<int> permissionGroupIds)
            where T : PermissionGroupMappingBase
        {
            if (GetHasPermissionMap().TryGetValue(typeof(T), out var delegateMethod))
            {
                return await delegateMethod(permissionGroupIds);
            }
            else
            {
                throw new OcudaException($"Unable to look up permissions for {typeof(T)}");
            }
        }

        public async Task RemoveApplicationPermissionGroupAsync(string applicationPermission,
            int permissionGroupId)
        {
            var permission = ApplicationPermissionDefinitions.ApplicationPermissions
                .SingleOrDefault(_ => string.Equals(_.Id, applicationPermission,
                    StringComparison.OrdinalIgnoreCase))
                ?? throw new OcudaException("Invalid application permission.");

            permissionGroupApplicationRepository.Remove(new PermissionGroupApplication
            {
                ApplicationPermission = applicationPermission,
                PermissionGroupId = permissionGroupId
            });
            await permissionGroupPageContentRepository.SaveAsync();
        }

        public async Task RemoveFromPermissionGroupAsync<T>(int itemId, int permissionGroupId)
            where T : PermissionGroupMappingBase
        {
            if (GetRemovePermissionMap().TryGetValue(typeof(T), out var delegateMethod))
            {
                await delegateMethod(itemId, permissionGroupId);
                _logger.LogInformation("User {CurrentUser} just added permission group id {PermissionGroupId} to {ItemType} id {ItemId}",
                    GetCurrentUserId(),
                    permissionGroupId,
                    typeof(T).Name,
                    itemId);
            }
            else
            {
                throw new OcudaException($"Unable to remove permissions for {typeof(T)}");
            }
        }

        public async Task SavePermissionGroups()
        {
            await permissionGroupRepository.SaveAsync();
        }

        public void UpdatePermissionGroup<T>(PermissionGroupMappingBase permissionGroup)
            where T : PermissionGroupMappingBase
        {
            ArgumentNullException.ThrowIfNull(permissionGroup);
            if (UpdatePermissionGroupMap().TryGetValue(typeof(T), out var delegateMethod))
            {
                delegateMethod(permissionGroup);
                _logger.LogInformation("User {CurrentUser} just updated permission group id {PermissionGroupId} to {ItemType}",
                    GetCurrentUserId(),
                    permissionGroup.PermissionGroupId,
                    typeof(T).Name);
            }
            else
            {
                throw new OcudaException($"Unable to update permissions for {typeof(T)}");
            }
        }

        private Dictionary<Type, Func<int, int, Task>> GetAddPermissionMap()
        {
            return new Dictionary<Type, Func<int, int, Task>>
            {
                { typeof(PermissionGroupIncidentLocation), async(_, __)
                    => await permissionGroupIncidentLocationRepository.AddSaveAsync(_, __) },
                { typeof(PermissionGroupPageContent), async (_, __)
                    => await permissionGroupPageContentRepository.AddSaveAsync(_, __) },
                { typeof(PermissionGroupPodcastItem), async (_, __)
                    => await permissionGroupPodcastItemRepository.AddSaveAsync(_, __) },
                { typeof(PermissionGroupProductManager), async(_, __)
                    => await permissionGroupProductManagerRepository.AddSaveAsync(_, __) },
                { typeof(PermissionGroupReplaceFiles), async(_, __)
                    => await permissionGroupReplaceFilesRepository.AddSaveAsync(_, __) },
                { typeof(PermissionGroupReporting), async(_, __)
                    => await permissionGroupReportingRepository.AddSaveAsync(_, __) },
                { typeof(PermissionGroupSectionManager), async(_, __)
                    => await permissionGroupSectionManagerRepository.AddSaveAsync(_, __) }
            };
        }

        private Dictionary<Type, Func<IEnumerable<int>, Task<bool>>> GetHasPermissionMap()
        {
            return new Dictionary<Type, Func<IEnumerable<int>, Task<bool>>>
            {
                { typeof(PermissionGroupIncidentLocation), async _
                    => await permissionGroupIncidentLocationRepository.AnyPermissionGroupIdAsync(_) },
                { typeof(PermissionGroupPodcastItem), async _
                    => await permissionGroupPodcastItemRepository.AnyPermissionGroupIdAsync(_) },
                { typeof(PermissionGroupPageContent), async _
                    => await permissionGroupPageContentRepository.AnyPermissionGroupIdAsync(_) },
                { typeof(PermissionGroupProductManager), async _
                    => await permissionGroupProductManagerRepository.AnyPermissionGroupIdAsync(_) },
                { typeof(PermissionGroupReplaceFiles), async _
                    => await permissionGroupReplaceFilesRepository.AnyPermissionGroupIdAsync(_) },
                { typeof(PermissionGroupReporting), async _
                    => await permissionGroupReportingRepository.AnyPermissionGroupIdAsync(_) },
                { typeof(PermissionGroupSectionManager), async _
                    => await permissionGroupSectionManagerRepository.AnyPermissionGroupIdAsync(_) }
            };
        }

        private Dictionary<Type, Func<IEnumerable<int>, Task<IEnumerable<int>>>> GetItemAccessMap()
        {
            return new Dictionary<Type, Func<IEnumerable<int>, Task<IEnumerable<int>>>>
            {
                { typeof(PermissionGroupIncidentLocation), async _
                    => await permissionGroupIncidentLocationRepository.GetByPermissionGroupIdsAsync(_) },
                { typeof(PermissionGroupPodcastItem), async _
                    => await permissionGroupPodcastItemRepository.GetByPermissionGroupIdsAsync(_) },
                { typeof(PermissionGroupPageContent), async _
                    => await permissionGroupPageContentRepository.GetByPermissionGroupIdsAsync(_) },
                { typeof(PermissionGroupProductManager), async _
                    => await permissionGroupProductManagerRepository.GetByPermissionGroupIdsAsync(_) },
                { typeof(PermissionGroupReplaceFiles), async _
                    => await permissionGroupReplaceFilesRepository.GetByPermissionGroupIdsAsync(_) },
                { typeof(PermissionGroupReporting), async _
                    => await permissionGroupReportingRepository.GetByPermissionGroupIdsAsync(_) },
                { typeof(PermissionGroupSectionManager), async _
                    => await permissionGroupSectionManagerRepository.GetByPermissionGroupIdsAsync(_) }
            };
        }

        private Dictionary<Type, Func<int, Task<object>>> GetLookupPermissionMap()
        {
            return new Dictionary<Type, Func<int, Task<object>>>
            {
                { typeof(PermissionGroupIncidentLocation), async _
                    => await permissionGroupIncidentLocationRepository.GetByLocationIdAsync(_) },
                { typeof(PermissionGroupPodcastItem), async _
                    => await permissionGroupPodcastItemRepository.GetByPodcastId(_) },
                { typeof(PermissionGroupPageContent), async _
                    => await permissionGroupPageContentRepository.GetByPageHeaderId(_) },
                { typeof(PermissionGroupProductManager), async _
                    => await permissionGroupProductManagerRepository.GetByProductIdAsync(_) },
                { typeof(PermissionGroupReplaceFiles), async _
                    => await permissionGroupReplaceFilesRepository.GetByFileLibraryId(_) },
                { typeof(PermissionGroupReporting), async _
                    => await permissionGroupReportingRepository.GetByReportIdAsync(_) },
                { typeof(PermissionGroupSectionManager), async _
                    => await permissionGroupSectionManagerRepository.GetBySectionIdAsync(_) }
            };
        }

        private Dictionary<Type, Func<int, int, Task>> GetRemovePermissionMap()
        {
            return new Dictionary<Type, Func<int, int, Task>>
            {
                { typeof(PermissionGroupIncidentLocation),async (_, __)
                    => await permissionGroupIncidentLocationRepository.RemoveSaveAsync(_, __) },
                { typeof(PermissionGroupPodcastItem),async (_, __)
                    => await permissionGroupPodcastItemRepository.RemoveSaveAsync(_, __) },
                { typeof(PermissionGroupPageContent),async (_, __)
                    => await permissionGroupPageContentRepository.RemoveSaveAsync(_, __) },
                { typeof(PermissionGroupProductManager), async(_, __)
                    => await permissionGroupProductManagerRepository.RemoveSaveAsync(_, __) },
                { typeof(PermissionGroupReplaceFiles), async(_, __)
                    => await permissionGroupReplaceFilesRepository.RemoveSaveAsync(_, __) },
                { typeof(PermissionGroupReporting), async(_, __)
                    => await permissionGroupReportingRepository.RemoveSaveAsync(_, __) },
                { typeof(PermissionGroupSectionManager), async(_, __)
                    => await permissionGroupSectionManagerRepository.RemoveSaveAsync(_, __) }
            };
        }

        private Dictionary<Type, Action<PermissionGroupMappingBase>> UpdatePermissionGroupMap()
        {
            return new Dictionary<Type, Action<PermissionGroupMappingBase>> {
                { typeof(PermissionGroupReporting),
                    _ => permissionGroupReportingRepository.Update((PermissionGroupReporting)_) }
            };
        }

        private async Task ValidateAsync(PermissionGroup permissionGroup)
        {
            if (await permissionGroupRepository.IsDuplicateAsync(permissionGroup))
            {
                throw new OcudaException($"Permission group '{permissionGroup.PermissionGroupName}' already exists.");
            }
        }
    }
}