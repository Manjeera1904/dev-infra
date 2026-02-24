namespace Aspire.Hosting;

static class AspireHostingExtensions
{
    public static IResourceBuilder<Resource> AddGroup(this IDistributedApplicationBuilder builder, string name) =>
        builder.AddResource(new GroupResource(name))
               .WithInitialState(new()
               {
                   State = new(KnownResourceStates.Running, KnownResourceStateStyles.Success),
                   ResourceType = "Group",
                   Properties = []
               });

    public static IResourceBuilder<T> InGroup<T>(this IResourceBuilder<T> builder, IResourceBuilder<IResource> group)
        where T : IResource
    {
        if (builder.Resource.TryGetAnnotationsOfType<ResourceSnapshotAnnotation>(out var annot))
        {
            foreach (var snapshot in annot)
            {
                snapshot.InitialSnapshot.GetType().GetProperty("Properties")?.SetValue(snapshot.InitialSnapshot,
                                                                                       snapshot.InitialSnapshot.Properties.Add(new("resource.parentName", "data")));
            }
        }
        else
        {
            builder.WithInitialState(new()
            {
                ResourceType = builder.Resource.GetType().Name ?? "Unknown",
                Properties =
                                         [
                                             new("resource.parentName", group.Resource.Name),
                                         ]
            });
        }

        return builder;
    }


    class GroupResource(string name) : Resource(name);
}
