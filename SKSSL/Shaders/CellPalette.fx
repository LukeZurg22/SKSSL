#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// SpriteBatch supplies the cell-ID texture as its drawn texture.
// Keep this as the first texture/sampler.
Texture2D SpriteTexture;

sampler2D SpriteTextureSampler = sampler_state
{
    Texture = <SpriteTexture>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = Point;
    AddressU = Clamp;
    AddressV = Clamp;
};

// One color per cell, stored in row-major order.
Texture2D PaletteTexture;

sampler2D PaletteSampler = sampler_state
{
    Texture = <PaletteTexture>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = Point;
    AddressU = Clamp;
    AddressV = Clamp;
};

// Original image. Used for pixels that were not assigned to a cell.
Texture2D BaseTexture;

sampler2D BaseSampler = sampler_state
{
    Texture = <BaseTexture>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = Point;
    AddressU = Clamp;
    AddressV = Clamp;
};

float PaletteWidth;
float PaletteHeight;

struct PixelInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 UV : TEXCOORD0;
};

float4 MainPS(PixelInput input) : COLOR
{
    float4 idPixel = tex2D(SpriteTextureSampler, input.UV);

    // Alpha < 0.25: preserve the original source pixel.
    if (idPixel.a < 0.25)
    {
        return tex2D(BaseSampler, input.UV) * input.Color;
    }

    // Alpha between 0.25 and 0.75: removed/empty pixel.
    if (idPixel.a < 0.75)
    {
        return float4(0, 0, 0, 0);
    }

    // Decode a 24-bit integer from RGB bytes.
    float3 bytes = floor(idPixel.rgb * 255.0 + 0.5);

    float cellId = bytes.r + bytes.g * 256.0 + bytes.b * 65536.0;

    // Convert the linear cell ID to a 2D palette coordinate.
    float paletteX = cellId - floor(cellId / PaletteWidth) * PaletteWidth;
    float paletteY = floor(cellId / PaletteWidth);

    // Sample at the texel center to prevent interpolation.
    float2 paletteUV = (float2(paletteX, paletteY) + 0.5) / float2(PaletteWidth, PaletteHeight);
    return tex2D(PaletteSampler, paletteUV) * input.Color;
}

technique SpriteDrawing
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};