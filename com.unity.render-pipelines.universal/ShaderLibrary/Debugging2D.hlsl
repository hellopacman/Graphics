
#ifndef UNIVERSAL_DEBUGGING2D_INCLUDED
#define UNIVERSAL_DEBUGGING2D_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/InputData2D.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/SurfaceData2D.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DebuggingCommon.hlsl"

#if defined(_DEBUG_SHADER)

#define SETUP_DEBUG_TEXTURE_DATA(inputData, positionWS, texture)    SetupDebugDataTexture(inputData, positionWS, texture##_TexelSize, texture##_MipInfo, GetMipCount(texture))
#define SETUP_DEBUG_DATA(inputData, positionWS)                     SetupDebugData(inputData, positionWS)

void SetupDebugData(inout InputData2D inputData, float3 positionWS)
{
    inputData.positionWS = positionWS;
}

void SetupDebugDataTexture(inout InputData2D inputData, float3 positionWS, float4 texelSize, float4 mipInfo, uint mipCount)
{
    SetupDebugData(inputData, positionWS);

    inputData.texelSize = texelSize;
    inputData.mipInfo = mipInfo;
    inputData.mipCount = mipCount;
}

bool CalculateDebugColorMaterialSettings(in SurfaceData2D surfaceData, in InputData2D inputData, out half4 debugColor)
{
    switch(_DebugMaterialMode)
    {
        case DEBUGMATERIALMODE_NONE:
        {
            debugColor = 0;
            return false;
        }

        case DEBUGMATERIALMODE_ALBEDO:
        {
            debugColor = half4(surfaceData.albedo, 1);
            return true;
        }

        case DEBUGMATERIALMODE_ALPHA:
        {
            debugColor = half4(surfaceData.alpha.rrr, 1);
            return true;
        }

        case DEBUGMATERIALMODE_SPRITE_MASK:
        {
            debugColor = surfaceData.mask;
            return true;
        }

        case DEBUGMATERIALMODE_NORMAL_TANGENT_SPACE:
        case DEBUGMATERIALMODE_NORMAL_WORLD_SPACE:
        {
            debugColor = half4(surfaceData.normalTS, 1);
            return true;
        }

        default:
        {
            debugColor = _DebugColorInvalidMode;
            return true;
        }
    }
}

bool CalculateDebugColorForRenderingSettings(in SurfaceData2D surfaceData, in InputData2D inputData, out half4 debugColor)
{
    if(CalculateColorForDebugSceneOverride(debugColor))
    {
        return true;
    }
    else
    {
        switch(_DebugMipInfoMode)
        {
            case DEBUGMIPINFOMODE_LEVEL:
            {
                debugColor = GetMipLevelDebugColor(inputData.positionWS, surfaceData.albedo, inputData.uv, inputData.texelSize);
                return true;
            }

            case DEBUGMIPINFOMODE_COUNT:
            {
                debugColor = GetMipCountDebugColor(inputData.positionWS, surfaceData.albedo, inputData.mipCount);
                return true;
            }

            default:
            {
                debugColor = 0;
                return false;
            }
        }
    }
}

bool CalculateDebugColorLightingSettings(in SurfaceData2D surfaceData, in InputData2D inputData, out half4 debugColor)
{
    switch(_DebugLightingMode)
    {
        case DEBUGLIGHTINGMODE_SHADOW_CASCADES:
        case DEBUGLIGHTINGMODE_REFLECTIONS:
        case DEBUGLIGHTINGMODE_REFLECTIONS_WITH_SMOOTHNESS:
        {
            debugColor = _DebugColorInvalidMode;
            return true;
        }

        default:
        {
            debugColor = 0;
            return false;
        }
    }       // End of switch.
}

bool CalculateDebugColorValidationSettings(in SurfaceData2D surfaceData, in InputData2D inputData, out half4 debugColor)
{
    switch(_DebugValidationMode)
    {
        case DEBUGVALIDATIONMODE_VALIDATE_ALBEDO:
        {
            return CalculateValidationAlbedo(surfaceData.albedo, debugColor);
        }

        case DEBUGVALIDATIONMODE_VALIDATE_MIPMAPS:
        {
            return CalculateValidationMipLevel(inputData.mipInfo.w, inputData.uv, inputData.texelSize, surfaceData.albedo, surfaceData.alpha, debugColor);
        }

        case DEBUGVALIDATIONMODE_VALIDATE_METALLIC:
        {
            debugColor = _DebugColorInvalidMode;
            return true;
        }

        default:
        {
            debugColor = 0;
            return false;
        }
    }
}

bool CalculateDebugColor(in SurfaceData2D surfaceData, in InputData2D inputData, out half4 debugColor)
{
    if(CalculateDebugColorMaterialSettings(surfaceData, inputData, debugColor))
    {
        return true;
    }
    else if(CalculateDebugColorForRenderingSettings(surfaceData, inputData, debugColor))
    {
        return true;
    }
    else if(CalculateDebugColorLightingSettings(surfaceData, inputData, debugColor))
    {
        return true;
    }
    else if(CalculateDebugColorValidationSettings(surfaceData, inputData, debugColor))
    {
        return true;
    }
    else
    {
        debugColor = 0;
        return false;
    }
}

#else

#define SETUP_DEBUG_TEXTURE_DATA(inputData, positionWS, texture)
#define SETUP_DEBUG_DATA(inputData, positionWS)

#endif

#endif
