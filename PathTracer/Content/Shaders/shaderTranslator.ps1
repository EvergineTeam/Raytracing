..\..\..\Binaries\dxc.exe -Zpr -fvk-use-dx-layout -fvk-u-shift 20 all -fvk-s-shift 40 all -fvk-t-shift 60 all -spirv -fspv-target-env='vulkan1.2' .\HLSL\HLSL.fx -T lib_6_3 -Fo VK\raytracing.spirv

Write-Host -NoNewLine 'Press any key to continue...';