using Common;
using Evergine.Common.Graphics;
using Evergine.Common.Graphics.Raytracing;
using Evergine.Mathematics;
using ImGuiNET;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Buffer = Evergine.Common.Graphics.Buffer;

namespace PathTracer
{
    public class PathTracerTest : VisualTestDefinition
    {
        private CommandQueue graphicCommandQueue;
        private uint width;
        private uint height;
        private RaytracingPipelineState pipelineState;
        private ResourceSet resourceSet;
        private Texture output;
        private TopLevelAS topLevel;
        private Buffer worldInfoCB;
        private WorldInfo worldInfo;

        private Viewport[] viewports;
        private ImGuiRenderer uiRenderer;
        private uint pathTracerSampleIndex;
        private int pathTracerNumSamples = 128;

        // Cache
        private int lastHash;

        private static readonly Vector2[] HaltonSequence = new[]
       {
                new Vector2(5.000000e-01f, 6.666667e-01f),
                new Vector2(2.500000e-01f, 3.333333e-01f),
                new Vector2(7.500000e-01f, 2.222222e-01f),
                new Vector2(1.250000e-01f, 8.888889e-01f),
                new Vector2(6.250000e-01f, 5.555556e-01f),
                new Vector2(3.750000e-01f, 1.111111e-01f),
                new Vector2(8.750000e-01f, 7.777778e-01f),
                new Vector2(6.250000e-02f, 4.444444e-01f),
                new Vector2(5.625000e-01f, 7.407407e-02f),
                new Vector2(3.125000e-01f, 7.407407e-01f),
                new Vector2(8.125000e-01f, 4.074074e-01f),
                new Vector2(1.875000e-01f, 2.962963e-01f),
                new Vector2(6.875000e-01f, 9.629630e-01f),
                new Vector2(4.375000e-01f, 6.296296e-01f),
                new Vector2(9.375000e-01f, 1.851852e-01f),
                new Vector2(3.125000e-02f, 8.518519e-01f),
                new Vector2(5.312500e-01f, 5.185185e-01f),
                new Vector2(2.812500e-01f, 3.703704e-02f),
                new Vector2(7.812500e-01f, 7.037037e-01f),
                new Vector2(1.562500e-01f, 3.703704e-01f),
                new Vector2(6.562500e-01f, 2.592593e-01f),
                new Vector2(4.062500e-01f, 9.259259e-01f),
                new Vector2(9.062500e-01f, 5.925926e-01f),
                new Vector2(9.375000e-02f, 1.481481e-01f),
                new Vector2(5.937500e-01f, 8.148148e-01f),
                new Vector2(3.437500e-01f, 4.814815e-01f),
                new Vector2(8.437500e-01f, 2.469136e-02f),
                new Vector2(2.187500e-01f, 6.913580e-01f),
                new Vector2(7.187500e-01f, 3.580247e-01f),
                new Vector2(4.687500e-01f, 2.469136e-01f),
                new Vector2(9.687500e-01f, 9.135802e-01f),
                new Vector2(1.562500e-02f, 5.802469e-01f),
                new Vector2(5.156250e-01f, 1.358025e-01f),
                new Vector2(2.656250e-01f, 8.024691e-01f),
                new Vector2(7.656250e-01f, 4.691358e-01f),
                new Vector2(1.406250e-01f, 9.876543e-02f),
                new Vector2(6.406250e-01f, 7.654321e-01f),
                new Vector2(3.906250e-01f, 4.320988e-01f),
                new Vector2(8.906250e-01f, 3.209877e-01f),
                new Vector2(7.812500e-02f, 9.876543e-01f),
                new Vector2(5.781250e-01f, 6.543210e-01f),
                new Vector2(3.281250e-01f, 2.098765e-01f),
                new Vector2(8.281250e-01f, 8.765432e-01f),
                new Vector2(2.031250e-01f, 5.432099e-01f),
                new Vector2(7.031250e-01f, 6.172840e-02f),
                new Vector2(4.531250e-01f, 7.283951e-01f),
                new Vector2(9.531250e-01f, 3.950617e-01f),
                new Vector2(4.687500e-02f, 2.839506e-01f),
                new Vector2(5.468750e-01f, 9.506173e-01f),
                new Vector2(2.968750e-01f, 6.172840e-01f),
                new Vector2(7.968750e-01f, 1.728395e-01f),
                new Vector2(1.718750e-01f, 8.395062e-01f),
                new Vector2(6.718750e-01f, 5.061728e-01f),
                new Vector2(4.218750e-01f, 1.234568e-02f),
                new Vector2(9.218750e-01f, 6.790123e-01f),
                new Vector2(1.093750e-01f, 3.456790e-01f),
                new Vector2(6.093750e-01f, 2.345679e-01f),
                new Vector2(3.593750e-01f, 9.012346e-01f),
                new Vector2(8.593750e-01f, 5.679012e-01f),
                new Vector2(2.343750e-01f, 1.234568e-01f),
                new Vector2(7.343750e-01f, 7.901235e-01f),
                new Vector2(4.843750e-01f, 4.567901e-01f),
                new Vector2(9.843750e-01f, 8.641975e-02f),
                new Vector2(7.812500e-03f, 7.530864e-01f),
                new Vector2(5.078125e-01f, 4.197531e-01f),
                new Vector2(2.578125e-01f, 3.086420e-01f),
                new Vector2(7.578125e-01f, 9.753086e-01f),
                new Vector2(1.328125e-01f, 6.419753e-01f),
                new Vector2(6.328125e-01f, 1.975309e-01f),
                new Vector2(3.828125e-01f, 8.641975e-01f),
                new Vector2(8.828125e-01f, 5.308642e-01f),
                new Vector2(7.031250e-02f, 4.938272e-02f),
                new Vector2(5.703125e-01f, 7.160494e-01f),
                new Vector2(3.203125e-01f, 3.827160e-01f),
                new Vector2(8.203125e-01f, 2.716049e-01f),
                new Vector2(1.953125e-01f, 9.382716e-01f),
                new Vector2(6.953125e-01f, 6.049383e-01f),
                new Vector2(4.453125e-01f, 1.604938e-01f),
                new Vector2(9.453125e-01f, 8.271605e-01f),
                new Vector2(3.906250e-02f, 4.938272e-01f),
                new Vector2(5.390625e-01f, 8.230453e-03f),
                new Vector2(2.890625e-01f, 6.748971e-01f),
                new Vector2(7.890625e-01f, 3.415638e-01f),
                new Vector2(1.640625e-01f, 2.304527e-01f),
                new Vector2(6.640625e-01f, 8.971193e-01f),
                new Vector2(4.140625e-01f, 5.637860e-01f),
                new Vector2(9.140625e-01f, 1.193416e-01f),
                new Vector2(1.015625e-01f, 7.860082e-01f),
                new Vector2(6.015625e-01f, 4.526749e-01f),
                new Vector2(3.515625e-01f, 8.230453e-02f),
                new Vector2(8.515625e-01f, 7.489712e-01f),
                new Vector2(2.265625e-01f, 4.156379e-01f),
                new Vector2(7.265625e-01f, 3.045267e-01f),
                new Vector2(4.765625e-01f, 9.711934e-01f),
                new Vector2(9.765625e-01f, 6.378601e-01f),
                new Vector2(2.343750e-02f, 1.934156e-01f),
                new Vector2(5.234375e-01f, 8.600823e-01f),
                new Vector2(2.734375e-01f, 5.267490e-01f),
                new Vector2(7.734375e-01f, 4.526749e-02f),
                new Vector2(1.484375e-01f, 7.119342e-01f),
                new Vector2(6.484375e-01f, 3.786008e-01f),
                new Vector2(3.984375e-01f, 2.674897e-01f),
                new Vector2(8.984375e-01f, 9.341564e-01f),
                new Vector2(8.593750e-02f, 6.008230e-01f),
                new Vector2(5.859375e-01f, 1.563786e-01f),
                new Vector2(3.359375e-01f, 8.230453e-01f),
                new Vector2(8.359375e-01f, 4.897119e-01f),
                new Vector2(2.109375e-01f, 3.292181e-02f),
                new Vector2(7.109375e-01f, 6.995885e-01f),
                new Vector2(4.609375e-01f, 3.662551e-01f),
                new Vector2(9.609375e-01f, 2.551440e-01f),
                new Vector2(5.468750e-02f, 9.218107e-01f),
                new Vector2(5.546875e-01f, 5.884774e-01f),
                new Vector2(3.046875e-01f, 1.440329e-01f),
                new Vector2(8.046875e-01f, 8.106996e-01f),
                new Vector2(1.796875e-01f, 4.773663e-01f),
                new Vector2(6.796875e-01f, 1.069959e-01f),
                new Vector2(4.296875e-01f, 7.736626e-01f),
                new Vector2(9.296875e-01f, 4.403292e-01f),
                new Vector2(1.171875e-01f, 3.292181e-01f),
                new Vector2(6.171875e-01f, 9.958848e-01f),
                new Vector2(3.671875e-01f, 6.625514e-01f),
                new Vector2(8.671875e-01f, 2.181070e-01f),
                new Vector2(2.421875e-01f, 8.847737e-01f),
                new Vector2(7.421875e-01f, 5.514403e-01f),
                new Vector2(4.921875e-01f, 6.995885e-02f),
                new Vector2(9.921875e-01f, 7.366255e-01f),
        };

        [StructLayout(LayoutKind.Explicit, Size = 208)]
        public struct WorldInfo
        {
            [FieldOffset(0)]
            public Vector3 CameraPosition;

            [FieldOffset(12)]
            public int NumBounces;

            [FieldOffset(16)]
            public Vector4 LightAmbientColor;

            [FieldOffset(32)]
            public Vector3 LightPosition;

            [FieldOffset(44)]
            public int NumRays;

            [FieldOffset(48)]
            public Vector4 LightDiffuseColor;

            [FieldOffset(64)]
            public Vector4 LightSpecularColor;

            [FieldOffset(80)]
            public float DiffuseCoef;

            [FieldOffset(84)]
            public float SpecularCoef;

            [FieldOffset(88)]
            public float SpecularPower;

            [FieldOffset(92)]
            public float InShadowRadiance;

            [FieldOffset(96)]
            public uint FrameCount;

            [FieldOffset(100)]
            public float LightRadius;

            [FieldOffset(104)]
            public uint PathTracerSampleIndex;

            [FieldOffset(108)]
            public float PathTracerAccumulationFactor;

            [FieldOffset(112)]
            public float AORadius;

            [FieldOffset(116)]
            public float AORayMin;

            [FieldOffset(120)]
            public Vector2 PixelOffset;

            [FieldOffset(128)]
            public float ReflectanceCoef;

            [FieldOffset(132)]
            public uint MaxRecursionDepth;

            [FieldOffset(136)]
            public float Roughness;

            [FieldOffset(144)]
            public Matrix4x4 CameraWorldViewProj;
        }

        public PathTracerTest()
            :base()
        {
        }

        public override void Initialize(GraphicsBackend backend)
        {
            base.Initialize(backend);

            if (backend == GraphicsBackend.Vulkan)
            {
                this.SwapChainPixelFormat = PixelFormat.B8G8R8A8_UNorm_SRgb;
            }
            else if (backend == GraphicsBackend.DirectX12)
            {
                this.SwapChainPixelFormat = PixelFormat.R16G16B16A16_Float;
            }

        }

        protected override async void InternalLoad()
        {
            if (!this.graphicsContext.Capabilities.IsRaytracingSupported)
            {
                throw new Exception("Not Raytracing supported");
            }

            CompilerParameters parameters = new CompilerParameters()
            {
                Profile = GraphicsProfile.Level_12_3,
                CompilationMode = CompilationMode.Debug,
            };
            GraphicsBackend backend = graphicsContext.BackendType;
            byte[] bytecode;
            switch (backend)
            {
                case GraphicsBackend.DirectX12:

                    var source = await this.assetsDirectory.ReadAsStringAsync($"Shaders/HLSL/HLSL.fx");
                    bytecode = graphicsContext.ShaderCompile(source, string.Empty, ShaderStages.RayGeneration, parameters).ByteCode;

                    break;
                case GraphicsBackend.Vulkan:

                    using (var stream = assetsDirectory.Open($"Shaders/VK/raytracing.spirv"))
                    using (var memstream = new MemoryStream())
                    {
                        stream.CopyTo(memstream);
                        bytecode = memstream.ToArray();
                    }

                    break;
                default:
                    throw new Exception("Backend no supported");
            }

            // Raygeneration shader
            var rayGenShaderDescription = new ShaderDescription(ShaderStages.RayGeneration, "rayGen", bytecode);
            Shader raygenerationShader = this.graphicsContext.Factory.CreateShader(ref rayGenShaderDescription);

            // Miss shader
            var missShaderDescription = new ShaderDescription(ShaderStages.Miss, "miss", bytecode);
            Shader missShader = this.graphicsContext.Factory.CreateShader(ref missShaderDescription);

            var missShadowShaderDescription = new ShaderDescription(ShaderStages.Miss, "missShadow", bytecode);
            Shader missShadowShader = this.graphicsContext.Factory.CreateShader(ref missShadowShaderDescription);

            var missAOShaderDescription = new ShaderDescription(ShaderStages.Miss, "AoMiss", bytecode);
            Shader missAOShader = this.graphicsContext.Factory.CreateShader(ref missAOShaderDescription);

            var missGIShaderDescription = new ShaderDescription(ShaderStages.Miss, "GIMiss", bytecode);
            Shader missGIShader = this.graphicsContext.Factory.CreateShader(ref missGIShaderDescription);

            // ClosestHit shader
            var closestHitShaderDescription = new ShaderDescription(ShaderStages.ClosestHit, "chs", bytecode);
            Shader closestHitShader = this.graphicsContext.Factory.CreateShader(ref closestHitShaderDescription);

            var closestHitShadowShaderDescription = new ShaderDescription(ShaderStages.ClosestHit, "shadowChs", bytecode);
            Shader closestHitShadowShader = this.graphicsContext.Factory.CreateShader(ref closestHitShadowShaderDescription);

            var closestHitAOShaderDescription = new ShaderDescription(ShaderStages.ClosestHit, "AOHit", bytecode);
            Shader closestHitAOShader = this.graphicsContext.Factory.CreateShader(ref closestHitAOShaderDescription);

            var closestHitGIShaderDescription = new ShaderDescription(ShaderStages.ClosestHit, "GIHit", bytecode);
            Shader closestHitGIShader = this.graphicsContext.Factory.CreateShader(ref closestHitGIShaderDescription);

            // ResourceLayout
            ResourceLayoutDescription layoutDescription = new ResourceLayoutDescription(
                                    new LayoutElementDescription(0, ResourceType.ConstantBuffer, ShaderStages.RayGeneration | ShaderStages.Miss | ShaderStages.ClosestHit),
                                    new LayoutElementDescription(0, ResourceType.TextureReadWrite, ShaderStages.RayGeneration),
                                    new LayoutElementDescription(0, ResourceType.AccelerationStructure, ShaderStages.RayGeneration | ShaderStages.ClosestHit),
                                    new LayoutElementDescription(1, ResourceType.StructuredBuffer, ShaderStages.RayGeneration | ShaderStages.ClosestHit),
                                    new LayoutElementDescription(2, ResourceType.StructuredBuffer, ShaderStages.RayGeneration | ShaderStages.ClosestHit),
                                    new LayoutElementDescription(3, ResourceType.StructuredBuffer, ShaderStages.RayGeneration | ShaderStages.ClosestHit),
                                    new LayoutElementDescription(4, ResourceType.Texture, ShaderStages.RayGeneration | ShaderStages.ClosestHit),
                                    new LayoutElementDescription(5, ResourceType.Texture, ShaderStages.RayGeneration | ShaderStages.ClosestHit),
                                    new LayoutElementDescription(0, ResourceType.Sampler, ShaderStages.RayGeneration | ShaderStages.ClosestHit)
                                    );
            ResourceLayout resourcesLayout = this.graphicsContext.Factory.CreateResourceLayout(ref layoutDescription);            

            // Raytracing Pipeline
            Trace.TraceInformation("Create a raytracing pipeline state object which defines the binding of shaders, state and resources to be used during raytracing ...");
            var pipelineDescription = new RaytracingPipelineDescription(
                                            new[] { resourcesLayout },
                                            new RaytracingShaderStateDescription()
                                            {
                                                RayGenerationShader = raygenerationShader,
                                                ClosestHitShader = new[] { closestHitShader, closestHitShadowShader, closestHitAOShader, closestHitGIShader },
                                                MissShader = new[] { missShader, missShadowShader, missAOShader, missGIShader },
                                            },
                                            new HitGroupDescription[] {
                                                    new HitGroupDescription()
                                                    {
                                                        Name = "RaygenGroup",
                                                        Type = HitGroupDescription.HitGroupType.General,
                                                        GeneralEntryPoint = "rayGen",
                                                    },
                                                    new HitGroupDescription()
                                                    {
                                                        Name = "MissGroup",
                                                        Type = HitGroupDescription.HitGroupType.General,
                                                        GeneralEntryPoint = "miss",
                                                    },
                                                    new HitGroupDescription()
                                                    {
                                                        Name = "MissShadowGroup",
                                                        Type = HitGroupDescription.HitGroupType.General,
                                                        GeneralEntryPoint = "missShadow",
                                                    },
                                                    new HitGroupDescription()
                                                    {
                                                        Name = "MissAOGroup",
                                                        Type = HitGroupDescription.HitGroupType.General,
                                                        GeneralEntryPoint = "AoMiss",
                                                    },
                                                    new HitGroupDescription()
                                                    {
                                                        Name = "MissGIGroup",
                                                        Type = HitGroupDescription.HitGroupType.General,
                                                        GeneralEntryPoint = "GIMiss",
                                                    },
                                                    new HitGroupDescription()
                                                    {
                                                        Name = "HitGroup",
                                                        Type = HitGroupDescription.HitGroupType.Triangles,
                                                        ClosestHitEntryPoint = "chs",
                                                    },
                                                    new HitGroupDescription()
                                                    {
                                                        Name = "ShadowHitGroup",
                                                        Type = HitGroupDescription.HitGroupType.Triangles,
                                                        ClosestHitEntryPoint = "shadowChs",
                                                    },
                                                    new HitGroupDescription()
                                                    {
                                                        Name = "AOHitGroup",
                                                        Type = HitGroupDescription.HitGroupType.Triangles,
                                                        ClosestHitEntryPoint = "AOHit",
                                                    },
                                                     new HitGroupDescription()
                                                    {
                                                        Name = "GIHitGroup",
                                                        Type = HitGroupDescription.HitGroupType.Triangles,
                                                        ClosestHitEntryPoint = "GIHit",
                                                    }
                                            },
                                            6, //  ~ primary rays only (Max recursion depth)
                                            sizeof(float) * 5, // float4 color (Max Payload size in bytes)
                                            sizeof(float) * 2 // float2 barycentrics (Max attribute size in bytes)
                                            );

            this.pipelineState = this.graphicsContext.Factory.CreateRaytracingPipeline(ref pipelineDescription);

            // Create Geometry
            Trace.TraceInformation("Build geometry to be used in the sample ...");

            // Load gltf mesh      
            this.LoadGLTF("Models/Room.gltf",
                          out Buffer roomIndexBuffer,
                          out int roomIndexCount,
                          out Buffer roomPositions,
                          out Buffer roomNormals,
                          out Buffer roomTangents,
                          out Buffer roomTexcoords);

            //-------------------------------
            // Create Acceleration Structures
            //-------------------------------
            Trace.TraceInformation("Build raytracing acceleration structures from the generated geometry ...");
            this.graphicCommandQueue = this.graphicsContext.Factory.CreateCommandQueue(CommandQueueType.Graphics);
            var commandBuffer = this.graphicCommandQueue.CommandBuffer();

            commandBuffer.Begin();

            //-------------------------------
            // Create Bottom Level Acceleration Structure
            //-------------------------------

            // Geometry0
            AccelerationStructureGeometry geometry0 = new AccelerationStructureTriangles()
            {
                Flags = AccelerationStructureGeometryFlags.Opaque,
                VertexBuffer = roomPositions,
                VertexFormat = PixelFormat.R32G32B32_Float,
                VertexStride = (uint)Unsafe.SizeOf<Vector3>(),
                VertexCount = roomPositions.Description.SizeInBytes / (uint)Unsafe.SizeOf<Vector3>(), // Positions
                IndexBuffer = roomIndexBuffer,
                IndexFormat = IndexFormat.UInt16,
                IndexCount = (uint)roomIndexCount,
            };

            BottomLevelASDescription bottomLevel0 = new BottomLevelASDescription()
            {
                Geometries = new AccelerationStructureGeometry[] { geometry0 },
            };

            // Bottom level0
            var blas0 = commandBuffer.BuildRaytracingAccelerationStructure(bottomLevel0);

            //-------------------------------
            // Create Top Level Acceleration Structure
            //-------------------------------

            // Instance
            var matrix = Matrix4x4.CreateScale(10);
            AccelerationStructureInstance instance0 = new AccelerationStructureInstance()
            {
                InstanceID = 0,
                InstanceContributionToHitGroupIndex = 0,
                Flags = AccelerationStructureInstanceFlags.None,
                Transform4x4 = Matrix4x4.Transpose(matrix),
                BottonLevel = blas0,
                InstanceMask = 0xFF,
            };

            // Top level
            var topLevelDescription = new TopLevelASDescription()
            {
                Flags = AccelerationStructureFlags.AllowUpdate,
                Instances = new AccelerationStructureInstance[] { instance0 },
            };

            this.topLevel = commandBuffer.BuildRaytracingAccelerationStructure(topLevelDescription);

            commandBuffer.End();

            commandBuffer.Commit();

            this.graphicCommandQueue.Submit();
            this.graphicCommandQueue.WaitIdle();

            Trace.TraceInformation("Build shader tables, which define shaders and their local root arguments ...");

            //-------------------------------
            // ResourceSet
            //-------------------------------
            var swapChainDescription = this.swapChain?.SwapChainDescription;
            this.width = swapChainDescription.HasValue ? swapChainDescription.Value.Width : this.surface.Width;
            this.height = swapChainDescription.HasValue ? swapChainDescription.Value.Height : this.surface.Height;

            // Ouput Texture
            var textureDescription = new TextureDescription()
            {
                Type = TextureType.Texture2D,
                Usage = ResourceUsage.Default,
                Flags = TextureFlags.UnorderedAccess | TextureFlags.ShaderResource,
                Format = PixelFormat.R16G16B16A16_Float,
                Width = width,
                Height = height,
                Depth = 1,
                MipLevels = 1,
                ArraySize = 1,
                Faces = 1,
                CpuAccess = ResourceCpuAccess.None,
                SampleCount = TextureSampleCount.None,
            };

            this.output = this.graphicsContext.Factory.CreateTexture(ref textureDescription);

            // Constant Buffer
            Vector3 cameraPosition = new Vector3(2.05f, 2.0f, 1.53f);

            // WorldInfo Constant buffer
            this.worldInfo = new WorldInfo()
            {
                CameraPosition = cameraPosition,
                NumBounces = 1,
                LightAmbientColor = new Vector4(0.02f),
                LightPosition = new Vector3(4.44f, 2.07f, 0.22f),
                NumRays = 4,
                LightDiffuseColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f),
                LightSpecularColor = new Vector4(1, 1, 1, 1),
                DiffuseCoef = 0.9f,
                SpecularCoef = 0.7f,
                SpecularPower = 50,
                InShadowRadiance = 0.0f,
                FrameCount = 0,
                LightRadius = 0.03f,
                PathTracerSampleIndex = 0,
                PathTracerAccumulationFactor = 1.0f,
                AORadius = 0.4f,
                AORayMin = 0.01f,
                PixelOffset = Vector2.One * 0.5f,
                ReflectanceCoef = 0.9f,
                MaxRecursionDepth = 2,
                Roughness = 1,
                CameraWorldViewProj = this.CreateCameraMatrix(cameraPosition),
            };
            var worldInfoCBDescription = new BufferDescription((uint)Unsafe.SizeOf<WorldInfo>(),
                                                                  BufferFlags.ConstantBuffer,
                                                                  ResourceUsage.Default);
            this.worldInfoCB = this.graphicsContext.Factory.CreateBuffer(ref worldInfo, ref worldInfoCBDescription);

            // Create diffuse texture
            Texture diffuseTex = null;
            using (var stream = this.assetsDirectory.Open("Models/RoomColorsTex.png"))
            {
                if (stream != null)
                {
                    VisualTests.LowLevel.Images.Image image = VisualTests.LowLevel.Images.Image.Load(stream);
                    var albedoDescription = image.TextureDescription;
                    diffuseTex = graphicsContext.Factory.CreateTexture(image.DataBoxes, ref albedoDescription);
                }
            }

            // Create roughness texture
            Texture roughnessTex = null;
            using (var stream = this.assetsDirectory.Open("Models/RoomRoughnessTex.png"))
            {
                if (stream != null)
                {
                    VisualTests.LowLevel.Images.Image image = VisualTests.LowLevel.Images.Image.Load(stream);
                    var roughnessDescription = image.TextureDescription;
                    roughnessTex = graphicsContext.Factory.CreateTexture(image.DataBoxes, ref roughnessDescription);
                }
            }

            // Sampler
            SamplerStateDescription samplerDescription = SamplerStates.LinearWrap;
            var sampler = this.graphicsContext.Factory.CreateSamplerState(ref samplerDescription);

            // Resource Set
            ResourceSetDescription resourceSetDescription = new ResourceSetDescription(resourcesLayout,
                                                                                       worldInfoCB,
                                                                                       this.output,
                                                                                       this.topLevel,
                                                                                       roomIndexBuffer,
                                                                                       roomNormals,
                                                                                       roomTexcoords,
                                                                                       diffuseTex,
                                                                                       roughnessTex,
                                                                                       sampler);
            this.resourceSet = this.graphicsContext.Factory.CreateResourceSet(ref resourceSetDescription);

            // Imgui
            this.viewports = new Viewport[1];
            this.viewports[0] = new Viewport(0, 0, width, height);
            this.uiRenderer = new ImGuiRenderer(this.graphicsContext, this.surface, this.frameBuffer);

            this.MarkAsLoaded();
        }

        private void LoadGLTF(string filePath, out Buffer indexBuffer, out int indexCount, out Buffer positions, out Buffer normals, out Buffer tangents, out Buffer texcoords)
        {
            indexCount = 0;
            using (var gltf = new GLTFLoader(this.assetsDirectory, filePath))
            {
                var mesh = gltf.Meshes[0];

                // Index Buffer
                var indexBufferView = mesh.IndicesBufferView;
                var indexBufferDescription = new BufferDescription((uint)indexBufferView.ByteLength,
                                                                    BufferFlags.BufferStructured | BufferFlags.ShaderResource | BufferFlags.AccelerationStructure,
                                                                    ResourceUsage.Default,
                                                                    ResourceCpuAccess.None,
                                                                    Unsafe.SizeOf<ushort>());
                var indexPointer = gltf.Buffers[indexBufferView.Buffer].bufferPointer + indexBufferView.ByteOffset;
                indexCount = indexBufferView.ByteLength / sizeof(ushort);
                indexBuffer = this.graphicsContext.Factory.CreateBuffer(indexPointer, ref indexBufferDescription);

                // Vertex Buffer
                int vertexBufferCount = mesh.AttributeBufferView.Length;
                positions = null;
                normals = null;
                tangents = null;
                texcoords = null;
                var attributes = gltf.model.Meshes[0].Primitives[0].Attributes.ToArray();
                for (int i = 0; i < vertexBufferCount; i++)
                {
                    var vertexBufferView = mesh.AttributeBufferView[i];

                    var accessor = gltf.model.Accessors[attributes[i].Value];
                    var vertexBufferDescription = new BufferDescription((uint)(accessor.Count * vertexBufferView.ByteStride),
                                                                        BufferFlags.BufferStructured | BufferFlags.ShaderResource | BufferFlags.AccelerationStructure,
                                                                        ResourceUsage.Default,
                                                                        ResourceCpuAccess.None,
                                                                        vertexBufferView.ByteStride.Value);
                    var vertexPointer = gltf.Buffers[vertexBufferView.Buffer].bufferPointer + vertexBufferView.ByteOffset + accessor.ByteOffset;

                    switch (attributes[i].Key)
                    {
                        case "POSITION":
                            positions = this.graphicsContext.Factory.CreateBuffer(vertexPointer, ref vertexBufferDescription);
                            break;
                        case "NORMAL":
                            normals = this.graphicsContext.Factory.CreateBuffer(vertexPointer, ref vertexBufferDescription);
                            break;
                        case "TANGENT":
                            tangents = this.graphicsContext.Factory.CreateBuffer(vertexPointer, ref vertexBufferDescription);
                            break;
                        case "TEXCOORD_0":
                            texcoords = this.graphicsContext.Factory.CreateBuffer(vertexPointer, ref vertexBufferDescription);
                            break;
                    }
                }
            }
        }

        protected override void OnResized(uint width, uint height)
        {
            this.viewports[0] = new Viewport(0, 0, width, height);
            this.uiRenderer.WindowResized((int)width, (int)height, this.frameBuffer);
        }

        protected override void InternalDrawCallback(TimeSpan gameTime)
        {
            var commandBuffer = this.graphicCommandQueue.CommandBuffer();

            commandBuffer.Begin();

            //Update Top Level
            var currentHash = this.GetWorldInfoHash();
            if (lastHash != currentHash)
            {
                this.pathTracerSampleIndex = 0;
                lastHash = currentHash;
            }

            // Update frameCount
            this.worldInfo.FrameCount++;
            this.worldInfo.PathTracerSampleIndex = this.pathTracerSampleIndex;
            this.worldInfo.PathTracerAccumulationFactor = 1.0f / ((float)pathTracerSampleIndex + 1.0f);
            this.worldInfo.PixelOffset = HaltonSequence[this.worldInfo.FrameCount % HaltonSequence.Length];
            commandBuffer.UpdateBufferData(this.worldInfoCB, ref this.worldInfo);

            if (this.pathTracerSampleIndex < this.pathTracerNumSamples)
            {
                commandBuffer.SetRaytracingPipelineState(this.pipelineState);
                commandBuffer.SetResourceSet(this.resourceSet);
                commandBuffer.DispatchRays(new DispatchRaysDescription()
                {
                    Width = this.width,
                    Height = this.height,
                    Depth = 1,
                });

                this.pathTracerSampleIndex++;
            }

            commandBuffer.Blit(this.output, this.swapChain.GetCurrentFramebufferTexture());

            this.DrawUI(commandBuffer, gameTime);

            commandBuffer.End();
            commandBuffer.Commit();

            this.graphicCommandQueue.Submit();
            this.graphicCommandQueue.WaitIdle();

        }

        private void DrawUI(CommandBuffer commandBuffer, TimeSpan gameTime)
        {
            this.surface.MouseDispatcher.DispatchEvents();
            this.surface.KeyboardDispatcher.DispatchEvents();

            commandBuffer.SetViewports(this.viewports);

            this.uiRenderer.NewFrame(gameTime);

            ImGui.Begin("Path Tracing");

            float x = this.worldInfo.LightPosition.X;
            float y = this.worldInfo.LightPosition.Y;
            float z = this.worldInfo.LightPosition.Z;

            ImGui.SliderFloat("Camera Pos X", ref x, -10, 10);
            ImGui.SliderFloat("Camera Pos Y", ref y, -10, 10);
            ImGui.SliderFloat("Camera Pos Z", ref z, -10, 10);

            this.worldInfo.LightPosition.X = x;
            this.worldInfo.LightPosition.Y = y;
            this.worldInfo.LightPosition.Z = z;
            ImGui.SliderFloat("Light Radius", ref this.worldInfo.LightRadius, 0.0f, 0.2f);

            ImGui.SliderInt("AO Num Rays", ref this.worldInfo.NumRays, 0, 32);
            ImGui.SliderFloat("AO Radius", ref this.worldInfo.AORadius, 0.0f, 2.0f);

            ImGui.SliderInt("GI Num Bounces", ref this.worldInfo.NumBounces, 0, 3);

            ImGui.SliderFloat("Reflectance Coef", ref this.worldInfo.ReflectanceCoef, 0, 1);
            ImGui.SliderFloat("Roughness", ref this.worldInfo.Roughness, 0, 1);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.SliderInt("Num Samples", ref this.pathTracerNumSamples, 0, 1024);
            ImGui.ProgressBar((float)this.pathTracerSampleIndex / (float)this.pathTracerNumSamples);

            ImGui.End();

            this.uiRenderer.Render(commandBuffer);
        }

        private Matrix4x4 CreateCameraMatrix(Vector3 cameraPosition)
        {
            var view = Matrix4x4.CreateLookAt(cameraPosition, new Vector3(0, 0.5f, 0), Vector3.UnitY);
            var proj = Matrix4x4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, (float)this.frameBuffer.Width / (float)this.frameBuffer.Height, 0.1f, 100f);
            var viewProj = Matrix4x4.Multiply(view, proj);
            return Matrix4x4.Invert(viewProj);
        }

        private int GetWorldInfoHash()
        {
            int hash = worldInfo.LightPosition.GetHashCode();
            hash = (hash * 397) ^ worldInfo.LightRadius.GetHashCode();
            hash = (hash * 397) ^ worldInfo.NumRays.GetHashCode();
            hash = (hash * 397) ^ worldInfo.AORadius.GetHashCode();
            hash = (hash * 397) ^ worldInfo.NumBounces.GetHashCode();
            hash = (hash * 397) ^ worldInfo.ReflectanceCoef.GetHashCode();
            hash = (hash * 397) ^ worldInfo.Roughness.GetHashCode();

            return hash;
        }
    }
}
