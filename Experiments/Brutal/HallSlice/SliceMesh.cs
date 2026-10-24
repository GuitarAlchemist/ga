using Brutal;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

// A slice's vertices in a host-visible vertex buffer, made from the vertices and destroyed by Dispose. Live counts the
// meshes made and not yet disposed, so that a run can show that materializing scenes and dropping them leaves none.
internal sealed unsafe class SliceMesh : IDisposable
{
    private readonly Device device;
    private readonly VkDeviceMemory memory;
    private bool disposed;

    public SliceMesh(Device device, PhysicalDevice physical, Vertex[] vertices)
    {
        this.device = device;
        Count = vertices.Length;
        var size = new ByteSize((nuint)(Math.Max(vertices.Length, 1) * sizeof(Vertex)));
        Buffer = device.CreateBuffer(new VkBufferCreateInfo
        {
            Size = size,
            Usage = VkBufferUsageFlags.VertexBufferBit,
            SharingMode = VkSharingMode.Exclusive,
        }, null);
        memory = device.AllocateMemory(physical, Buffer, VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit, null);
        device.BindBufferMemory(Buffer, memory, memoryOffset: ByteSize.Zero);
        var mapped = device.MapMemory<Vertex>(new MapInfo(memory) { Region = new MapRegion { Size = size } });
        vertices.AsSpan().CopyTo(new Span<Vertex>((Vertex*)mapped, vertices.Length));
        device.UnmapMemory(memory);
        Live++;
    }

    public static int Live { get; private set; }

    public VkBuffer Buffer { get; }

    public int Count { get; }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        device.DestroyBuffer(Buffer, null);
        device.FreeMemory(memory, null);
        disposed = true;
        Live--;
    }
}
