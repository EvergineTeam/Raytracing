﻿using Evergine.Bindings.Imgui;
using Evergine.Common.Graphics;
using Evergine.Common.Input.Keyboard;
using Evergine.Common.Input.Mouse;
using Evergine.Mathematics;
using System.Runtime.CompilerServices;
using Buffer = Evergine.Common.Graphics.Buffer;

namespace Common
{
    public unsafe class ImGuiRenderer : IDisposable
    {
        private GraphicsContext graphicsContext;
        private Surface surface;

        private Buffer[] vertexBuffers;
        private Buffer indexBuffer;
        private Buffer constantBuffer;
        private Texture fontTexture;
        private SamplerState sampler;
        private GraphicsPipelineState pipelineState;
        private ResourceLayout layout;
        private ResourceSet resourceSet;
        private ImGuiIO* io;

        private int windowWidth;
        private int windowHeight;
        private Vector2 scaleFactor;

        private IntPtr fontAtlasID;
        private FrameBuffer framebuffer;

        private int lastAssignedID = 100;
        private Matrix4x4 mvp;

        private struct ResourceSetInfo
        {
            public readonly IntPtr ImGuiBinding;
            public readonly ResourceSet ResourceSet;

            public ResourceSetInfo(IntPtr imGuiBinding, ResourceSet resourceSet)
            {
                this.ImGuiBinding = imGuiBinding;
                this.ResourceSet = resourceSet;
            }
        }

        private readonly Dictionary<Texture, ResourceSetInfo> resourceByTexture = new Dictionary<Texture, ResourceSetInfo>();
        private readonly Dictionary<IntPtr, ResourceSetInfo> resourceById = new Dictionary<IntPtr, ResourceSetInfo>();

        public ImGuiRenderer(GraphicsContext context, Surface surface, FrameBuffer fb)
        {
            this.graphicsContext = context;
            this.surface = surface;
            this.framebuffer = fb;
            this.scaleFactor = Vector2.One;
            this.fontAtlasID = (IntPtr)1;

            this.InitializeImGui();
        }

        private unsafe void InitializeImGui()
        {
            IntPtr imGuiContext = ImguiNative.igCreateContext((ImFontAtlas*)null);
            ImguiNative.igSetCurrentContext(imGuiContext);

            this.io = ImguiNative.igGetIO();
            this.io->Fonts->AddFontDefault(null);

            // Compile shaders.
            var vsCode = this.NativeAPICompiler(ShaderStages.Vertex);
            var psCode = this.NativeAPICompiler(ShaderStages.Pixel);

            // Create native resources
            var vertexShaderDescription = new ShaderDescription(ShaderStages.Vertex, "VS", vsCode);
            var pixelShaderDescription = new ShaderDescription(ShaderStages.Pixel, "PS", psCode);

            var vertexShader = this.graphicsContext.Factory.CreateShader(ref vertexShaderDescription);
            var pixelShader = this.graphicsContext.Factory.CreateShader(ref pixelShaderDescription);

            var vertexBufferDescription = new BufferDescription(
                8192,
                BufferFlags.VertexBuffer,
                ResourceUsage.Dynamic,
                ResourceCpuAccess.Write);

            this.vertexBuffers = new Buffer[1];
            this.vertexBuffers[0] = this.graphicsContext.Factory.CreateBuffer(ref vertexBufferDescription);

            var indexBufferDescription = new BufferDescription(
                2048,
                BufferFlags.IndexBuffer,
                ResourceUsage.Dynamic,
                ResourceCpuAccess.Write);

            this.indexBuffer = this.graphicsContext.Factory.CreateBuffer(ref indexBufferDescription);

            var vertexLayouts = new InputLayouts()
                  .Add(new LayoutDescription()
                              .Add(new ElementDescription(ElementFormat.Float2, ElementSemanticType.Position))
                              .Add(new ElementDescription(ElementFormat.Float2, ElementSemanticType.TexCoord))
                              .Add(new ElementDescription(ElementFormat.UByte4Normalized, ElementSemanticType.Color)));

            var layoutDescription = new ResourceLayoutDescription(
                    new LayoutElementDescription(0, ResourceType.ConstantBuffer, ShaderStages.Vertex),
                    new LayoutElementDescription(0, ResourceType.Texture, ShaderStages.Pixel),
                    new LayoutElementDescription(0, ResourceType.Sampler, ShaderStages.Pixel));

            this.layout = this.graphicsContext.Factory.CreateResourceLayout(ref layoutDescription);

            // Create pipeline
            var blendState = BlendStates.AlphaBlend;
            blendState.AlphaToCoverageEnable = false;
            blendState.RenderTarget0.BlendEnable = true;
            blendState.RenderTarget0.SourceBlendColor = Blend.SourceAlpha;
            blendState.RenderTarget0.DestinationBlendColor = Blend.InverseSourceAlpha;
            blendState.RenderTarget0.BlendOperationColor = BlendOperation.Add;
            blendState.RenderTarget0.SourceBlendAlpha = Blend.SourceAlpha;
            blendState.RenderTarget0.DestinationBlendAlpha = Blend.Zero;
            blendState.RenderTarget0.BlendOperationAlpha = BlendOperation.Add;

            var rasterizerState = RasterizerStates.None;
            rasterizerState.FillMode = FillMode.Solid;
            rasterizerState.CullMode = CullMode.None;
            rasterizerState.ScissorEnable = true;
            rasterizerState.DepthClipEnable = true;

            var pipelineDescription = new GraphicsPipelineDescription()
            {
                PrimitiveTopology = PrimitiveTopology.TriangleList,
                InputLayouts = vertexLayouts,
                ResourceLayouts = new[] { this.layout },
                Shaders = new GraphicsShaderStateDescription()
                {
                    VertexShader = vertexShader,
                    PixelShader = pixelShader,
                },
                RenderStates = new RenderStateDescription()
                {
                    RasterizerState = rasterizerState,
                    BlendState = blendState,
                    DepthStencilState = DepthStencilStates.None,
                },
                Outputs = this.framebuffer.OutputDescription,
            };

            this.windowWidth = (int)this.framebuffer.Width;
            this.windowHeight = (int)this.framebuffer.Height;

            this.pipelineState = this.graphicsContext.Factory.CreateGraphicsPipeline(ref pipelineDescription);

            var constantBufferDescription = new BufferDescription((uint)Unsafe.SizeOf<Matrix4x4>(), BufferFlags.ConstantBuffer, ResourceUsage.Default);
            this.constantBuffer = this.graphicsContext.Factory.CreateBuffer(ref constantBufferDescription);

            // Create Font Texture
            int width;
            int height;
            int bytesPerPixel;
            byte* pixels = null;
            this.io->Fonts->GetTexDataAsRGBA32(&pixels, &width, &height, &bytesPerPixel);

            this.io->Fonts->SetTexID(this.fontAtlasID);

            var fontTextureDescription = new TextureDescription()
            {
                Type = TextureType.Texture2D,
                Width = (uint)width,
                Height = (uint)height,
                Format = PixelFormat.R8G8B8A8_UNorm,
                Usage = ResourceUsage.Default,
                Depth = 1,
                Faces = 1,
                ArraySize = 1,
                MipLevels = 1,
                SampleCount = TextureSampleCount.None,
                CpuAccess = ResourceCpuAccess.Write,
                Flags = TextureFlags.ShaderResource,
            };

            this.fontTexture = this.graphicsContext.Factory.CreateTexture(ref fontTextureDescription);
            this.graphicsContext.UpdateTextureData(this.fontTexture, (IntPtr)pixels, (uint)(bytesPerPixel * width * height), 0);

            SamplerStateDescription samplerDescription = new SamplerStateDescription()
            {
                Filter = TextureFilter.MinLinear_MagLinear_MipLinear,
                AddressU = TextureAddressMode.Wrap,
                AddressV = TextureAddressMode.Wrap,
                AddressW = TextureAddressMode.Wrap,
                MipLODBias = 0f,
                MaxAnisotropy = 1,
                ComparisonFunc = ComparisonFunction.Never,
                MinLOD = 0f,
                MaxLOD = 0f,
            };

            this.sampler = this.graphicsContext.Factory.CreateSamplerState(ref samplerDescription);

            var resourceSetDescription = new ResourceSetDescription(this.layout, this.constantBuffer, this.fontTexture, this.sampler);
            this.resourceSet = this.graphicsContext.Factory.CreateResourceSet(ref resourceSetDescription);

            this.io->Fonts->ClearTexData();

            // Keyboard mapping. ImGui will use those indices to peek into the io.KeyDown[] array that we will update during the application lifetime.
            this.io->KeyMap[(int)ImGuiKey.Tab] = (int)Keys.Tab;
            this.io->KeyMap[(int)ImGuiKey.LeftArrow] = (int)Keys.Left;
            this.io->KeyMap[(int)ImGuiKey.RightArrow] = (int)Keys.Right;
            this.io->KeyMap[(int)ImGuiKey.UpArrow] = (int)Keys.Up;
            this.io->KeyMap[(int)ImGuiKey.DownArrow] = (int)Keys.Down;
            this.io->KeyMap[(int)ImGuiKey.PageUp] = (int)Keys.PageUp;
            this.io->KeyMap[(int)ImGuiKey.PageDown] = (int)Keys.PageDown;
            this.io->KeyMap[(int)ImGuiKey.Home] = (int)Keys.Home;
            this.io->KeyMap[(int)ImGuiKey.End] = (int)Keys.End;
            this.io->KeyMap[(int)ImGuiKey.Delete] = (int)Keys.Delete;
            this.io->KeyMap[(int)ImGuiKey.Backspace] = (int)Keys.Back;
            this.io->KeyMap[(int)ImGuiKey.Enter] = (int)Keys.Enter;
            this.io->KeyMap[(int)ImGuiKey.Escape] = (int)Keys.Escape;
            this.io->KeyMap[(int)ImGuiKey.A] = (int)Keys.A;
            this.io->KeyMap[(int)ImGuiKey.C] = (int)Keys.C;
            this.io->KeyMap[(int)ImGuiKey.V] = (int)Keys.V;
            this.io->KeyMap[(int)ImGuiKey.X] = (int)Keys.X;
            this.io->KeyMap[(int)ImGuiKey.Y] = (int)Keys.Y;
            this.io->KeyMap[(int)ImGuiKey.Z] = (int)Keys.Z;

            // Register input events
            var mouseDispatcher = this.surface.MouseDispatcher;
            mouseDispatcher.MouseButtonDown += this.MouseDispatcher_MouseButtonDown;
            mouseDispatcher.MouseButtonUp += this.MouseDispatcher_MouseButtonUp;
            mouseDispatcher.MouseMove += this.MouseDispatcher_MouseMove;
            mouseDispatcher.MouseScroll += this.MouseDispatcher_MouseScroll;

            var keyboardDispatcher = this.surface.KeyboardDispatcher;
            keyboardDispatcher.KeyDown += this.KeyboardDispatcher_KeyDown;
            keyboardDispatcher.KeyUp += this.KeyboardDispatcher_KeyUp;
            keyboardDispatcher.KeyChar += this.KeyboardDispatcher_KeyChar;
        }

        private void KeyboardDispatcher_KeyChar(object sender, KeyCharEventArgs e)
        {
            this.io->AddInputCharacter(e.Character);
        }

        private void KeyboardDispatcher_KeyUp(object sender, KeyEventArgs e)
        {
            this.io->KeysDown[(int)e.Key] = 0;
        }

        private void KeyboardDispatcher_KeyDown(object sender, KeyEventArgs e)
        {
            this.io->KeysDown[(int)e.Key] = 1;
        }

        private void MouseDispatcher_MouseScroll(object sender, MouseScrollEventArgs e)
        {
            this.io->MouseWheel = e.Delta.Y;
        }

        private void MouseDispatcher_MouseMove(object sender, MouseEventArgs e)
        {
            this.io->MousePos.X = e.Position.X / this.surface.DPIDensity;
            this.io->MousePos.Y = e.Position.Y / this.surface.DPIDensity;
        }

        private void MouseDispatcher_MouseButtonUp(object sender, MouseButtonEventArgs e)
        {
            switch (e.Button)
            {
                case MouseButtons.Left:
                    this.io->MouseDown[0] = 0;
                    break;
                case MouseButtons.Right:
                    this.io->MouseDown[1] = 0;
                    break;
                case MouseButtons.Middle:
                    this.io->MouseDown[2] = 0;
                    break;
                default:
                    break;
            }
        }

        private void MouseDispatcher_MouseButtonDown(object sender, MouseButtonEventArgs e)
        {
            switch (e.Button)
            {
                case MouseButtons.Left:
                    this.io->MouseDown[0] = 1;
                    break;
                case MouseButtons.Right:
                    this.io->MouseDown[1] = 1;
                    break;
                case MouseButtons.Middle:
                    this.io->MouseDown[2] = 1;
                    break;
            }
        }

        private byte[] NativeAPICompiler(ShaderStages stage)
        {
            string shaderCode = string.Empty;
            string entryPoint = stage == ShaderStages.Vertex ? "VS" : "PS";
            byte[] byteCode = null;

            switch (this.graphicsContext.BackendType)
            {
                case GraphicsBackend.OpenGL:
                    shaderCode = stage == ShaderStages.Vertex ? Shaders.GLSLVertexShader : Shaders.GLSLPixelShader;
                    break;
                case GraphicsBackend.Metal:
                    shaderCode = stage == ShaderStages.Vertex ? Shaders.MSLVertexShader : Shaders.MSLPixelShader;
                    break;
                case GraphicsBackend.Vulkan:
                    string code = stage == ShaderStages.Vertex ? Shaders.SPIRVVertexShader : Shaders.SPIRVPixelShader;
                    byteCode = Convert.FromBase64String(code);
                    break;
                case GraphicsBackend.DirectX12:
                case GraphicsBackend.DirectX11:
                default:
                    shaderCode = stage == ShaderStages.Vertex ? Shaders.HLSLVertexShader : Shaders.HLSLPixelShader;
                    break;
            }

            if (!string.IsNullOrEmpty(shaderCode))
            {
                CompilerParameters parameters = new CompilerParameters()
                {
                    CompilationMode = CompilationMode.None,
                    Profile = GraphicsProfile.Level_11_0,
                };

                var result = this.graphicsContext.ShaderCompile(shaderCode, entryPoint, stage, parameters);
                if (!result.HasErrors)
                {
                    byteCode = result.ByteCode;
                }
            }

            return byteCode;
        }

        public void WindowResized(int width, int height, FrameBuffer fb)
        {
            this.windowWidth = width;
            this.windowHeight = height;
            this.framebuffer = fb;
        }

        public void NewFrame(TimeSpan gameTime)
        {
            this.io->DisplaySize = new Vector2(
                            this.windowWidth / this.scaleFactor.X,
                            this.windowHeight / this.scaleFactor.Y);

            this.io->DisplayFramebufferScale = this.scaleFactor;
            this.io->DeltaTime = (float)gameTime.TotalSeconds;

            // Read keyboard modifiers input
            var keyboardDispatcher = this.surface.KeyboardDispatcher;
            this.io->KeyCtrl = keyboardDispatcher.IsKeyDown(Keys.LeftControl) ? (byte)1 : (byte)0;
            this.io->KeyShift = keyboardDispatcher.IsKeyDown(Keys.LeftShift) ? (byte)1 : (byte)0;
            this.io->KeyAlt = keyboardDispatcher.IsKeyDown(Keys.LeftAlt) ? (byte)1 : (byte)0;

            // Set orthographics projection matrix
            this.mvp = Matrix4x4.CreateOrthographicOffCenter(
                0f,
                this.io->DisplaySize.X,
                this.io->DisplaySize.Y,
                0.0f,
                -1.0f,
                1.0f,
                this.graphicsContext.Capabilities.ClipDepth == ClipDepth.ZeroToOne);

            if (this.framebuffer.RequireFlipProjection)
            {
                this.mvp.M22 *= -1;
            }

            ImguiNative.igNewFrame();
        }

        public void Render(CommandBuffer commandBuffer)
        {
            ImguiNative.igRender();

            uint vertexOffsetInVertices = 0;
            uint indexOffsetInElements = 0;

            ImDrawData* drawData = ImguiNative.igGetDrawData();

            if (drawData->CmdListsCount > 0)
            {
                // Resize index and vertex buffers.
                int vertexBufferSize = drawData->TotalVtxCount * sizeof(ImDrawVert);
                if (vertexBufferSize > this.vertexBuffers[0].Description.SizeInBytes)
                {
                    this.vertexBuffers[0].Dispose();
                    uint nextSize = (uint)MathHelper.NextPowerOfTwo(vertexBufferSize);
                    var vertexBufferDescription = new BufferDescription(
                        nextSize,
                        BufferFlags.VertexBuffer,
                        ResourceUsage.Dynamic,
                        ResourceCpuAccess.Write);

                    this.vertexBuffers[0] = this.graphicsContext.Factory.CreateBuffer(ref vertexBufferDescription);
                }

                int indexBufferSize = drawData->TotalIdxCount * sizeof(ushort);
                if (indexBufferSize > this.indexBuffer.Description.SizeInBytes)
                {
                    this.indexBuffer.Dispose();
                    uint nextSize = (uint)MathHelper.NextPowerOfTwo(indexBufferSize);
                    var indexBufferDescription = new BufferDescription(
                        nextSize,
                        BufferFlags.IndexBuffer,
                        ResourceUsage.Dynamic,
                        ResourceCpuAccess.Write);

                    this.indexBuffer = this.graphicsContext.Factory.CreateBuffer(ref indexBufferDescription);
                }

                // Update index and vertex buffers.
                var vResource = this.graphicsContext.MapMemory(this.vertexBuffers[0], MapMode.Write);
                var iResource = this.graphicsContext.MapMemory(this.indexBuffer, MapMode.Write);

                for (int i = 0; i < drawData->CmdListsCount; i++)
                {
                    ImDrawList* cmdList = drawData->CmdLists[i];

                    // Copy vertex
                    var vOffset = vertexOffsetInVertices * (uint)sizeof(ImDrawVert);
                    Unsafe.CopyBlock((void*)((long)vResource.Data + vOffset), (void*)cmdList->VtxBuffer.Data, (uint)(cmdList->VtxBuffer.Size * sizeof(ImDrawVert)));

                    // Copy index
                    var iOffset = indexOffsetInElements * sizeof(ushort);
                    Unsafe.CopyBlock((void*)((long)iResource.Data + iOffset), (void*)cmdList->IdxBuffer.Data, (uint)(cmdList->IdxBuffer.Size * sizeof(ushort)));

                    vertexOffsetInVertices += (uint)cmdList->VtxBuffer.Size;
                    indexOffsetInElements += (uint)cmdList->IdxBuffer.Size;
                }

                this.graphicsContext.UnmapMemory(this.vertexBuffers[0]);
                this.graphicsContext.UnmapMemory(this.indexBuffer);

                commandBuffer.BeginDebugMarker("ImGUI");
                commandBuffer.UpdateBufferData(this.constantBuffer, ref this.mvp);

                RenderPassDescription renderPassDescription = new RenderPassDescription(this.framebuffer, ClearValue.None);
                commandBuffer.BeginRenderPass(ref renderPassDescription);

                // Bind resources
                commandBuffer.SetGraphicsPipelineState(this.pipelineState);
                commandBuffer.SetVertexBuffers(this.vertexBuffers);
                commandBuffer.SetIndexBuffer(this.indexBuffer, IndexFormat.UInt16);

                drawData->ScaleClipRects(this.io->DisplayFramebufferScale);

                // Render command lists
                uint vtx_offset = 0;
                uint idx_offset = 0;

                for (int n = 0; n < drawData->CmdListsCount; n++)
                {
                    ImDrawList* cmdList = drawData->CmdLists[n];
                    for (int i = 0; i < cmdList->CmdBuffer.Size; i++)
                    {
                        ImDrawCmd* cmd = cmdList->GetDrawCmdAt(i);
                        if (cmd->TextureId != IntPtr.Zero)
                        {
                            if (cmd->TextureId == this.fontAtlasID)
                            {
                                commandBuffer.SetResourceSet(this.resourceSet);
                            }
                            else
                            {
                                commandBuffer.SetResourceSet(this.GetImageResourceSet(cmd->TextureId), 1);
                            }
                        }

                        var scissors = new Rectangle[1]
                        {
                            new Rectangle(
                            (int)cmd->ClipRect.X,
                            (int)cmd->ClipRect.Y,
                            (int)(cmd->ClipRect.Z - cmd->ClipRect.X),
                            (int)(cmd->ClipRect.W - cmd->ClipRect.Y)),
                        };

                        commandBuffer.SetScissorRectangles(scissors);

                        commandBuffer.DrawIndexedInstanced(cmd->ElemCount, 1, idx_offset, vtx_offset, 0);

                        idx_offset += cmd->ElemCount;
                    }

                    vtx_offset += (uint)cmdList->VtxBuffer.Size;
                }

                commandBuffer.EndDebugMarker();
                commandBuffer.EndRenderPass();
            }
        }

        public IntPtr CreateImGuiBinding(Texture texture)
        {
            if (!this.resourceByTexture.TryGetValue(texture, out ResourceSetInfo info))
            {
                var resourceSetDescriptionnew = new ResourceSetDescription(this.layout, this.constantBuffer, texture, this.sampler);
                var newResourceSet = this.graphicsContext.Factory.CreateResourceSet(ref resourceSetDescriptionnew);
                info = new ResourceSetInfo(this.GetNextImGuiBindingID(), newResourceSet);

                this.resourceByTexture.Add(texture, info);
                this.resourceById.Add(info.ImGuiBinding, info);
            }

            return info.ImGuiBinding;
        }

        /// <summary>
        /// Remove a ImGui texture binding.
        /// </summary>
        /// <param name="texture">The texture to remove from binding list.</param>
        public void RemoveImGuiBinding(Texture texture)
        {
            if (this.resourceByTexture.TryGetValue(texture, out ResourceSetInfo info))
            {
                this.resourceByTexture.Remove(texture);
                this.resourceById.Remove(info.ImGuiBinding);
                info.ResourceSet.Dispose();
            }
        }

        private IntPtr GetNextImGuiBindingID()
        {
            int newID = this.lastAssignedID++;
            return (IntPtr)newID;
        }

        private ResourceSet GetImageResourceSet(IntPtr textureId)
        {
            if (this.resourceById.TryGetValue(textureId, out ResourceSetInfo rsi))
            {
                return rsi.ResourceSet;
            }

            return null;
        }

        public void Dispose()
        {
            foreach (var rsi in this.resourceById)
            {
                rsi.Value.ResourceSet.Dispose();
            }

            this.resourceByTexture.Clear();
            this.resourceById.Clear();

            ImguiNative.igDestroyContext(IntPtr.Zero);
            this.vertexBuffers[0].Dispose();
            this.vertexBuffers = null;
            this.indexBuffer.Dispose();
            this.constantBuffer.Dispose();
            this.layout.Dispose();
            this.resourceSet.Dispose();
        }
    }
}
