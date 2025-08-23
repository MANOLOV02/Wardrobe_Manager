Imports OpenTK.Graphics.OpenGL4
Imports OpenTK.Mathematics
Public Class Shader_Class
    Implements IDisposable

    Private disposedValue As Boolean
    Private ReadOnly vertexShaderSource As String = "
#version 440
uniform mat4 matProjection;
uniform mat4 matView;
uniform mat4 matModel;
uniform mat4 matModelView;
uniform mat3 mv_normalMatrix;
uniform vec3 color;
uniform vec3 subColor;

uniform bool bShowTexture;
uniform bool bShowMask;
uniform bool bShowWeight;
uniform bool bShowVertexColor;
uniform bool bShowVertexAlpha;
uniform bool bApplyZap;

uniform bool bWireframe;

layout(location = 0) in vec3 vertexPosition;
layout(location = 1) in vec3 vertexNormal;
layout(location = 2) in vec3 vertexTangent;
layout(location = 3) in vec3 vertexBitangent;
layout(location = 4) in vec3 vertexColors;
layout(location = 5) in float vertexAlpha;
layout(location = 6) in vec2 vertexUV;
layout(location = 7) in float vertexMask;
layout(location = 8) in float vertexWeight;

struct DirectionalLight
{
	vec3 diffuse;
	vec3 direction;
};

uniform DirectionalLight frontal;
uniform DirectionalLight directional0;
uniform DirectionalLight directional1;
uniform DirectionalLight directional2;

out vec3 lightFrontal;
out vec3 lightDirectional0;
out vec3 lightDirectional1;
out vec3 lightDirectional2;

out vec3 viewDir;
out mat3 mv_tbn;

out float maskFactor;
flat out int ZappedVert;
out vec3 weightColor;

out vec4 vColor;
out vec2 vUV;

vec3 colorRamp(in float value)
{
	float r;
	float g;
	float b;

	if (value <= 0.0f)
	{
		r = g = b = 1.0;
	}
	else if (value <= 0.25)
	{
		r = 0.0;
		b = 1.0;
		g = value / 0.25;
	}
	else if (value <= 0.5)
	{
		r = 0.0;
		g = 1.0;
		b = 1.0 + (-1.0) * (value - 0.25) / 0.25;
	}
	else if (value <= 0.75)
	{
		r = (value - 0.5) / 0.25;
		g = 1.0;
		b = 0.0;
	}
	else
	{
		r = 1.0;
		g = 1.0 + (-1.0) * (value - 0.75) / 0.25;
		b = 0.0;
	}

	return vec3(r, g, b);
}

void main(void)
{
	// Initialization
	maskFactor = 1.0;
    ZappedVert = 0;
    if (bApplyZap)
    {
     if (vertexMask<0)    
      ZappedVert = 1;
    }
	if (bShowMask)
	{
		maskFactor = 1.0 - vertexMask / 1.5;
    
    if (ZappedVert==1) //zapped
        {
    		maskFactor = 1.0 - (-vertexMask) / 1.5;
        }

   	}
	weightColor = vec3(1.0, 1.0, 1.0);
	vColor = vec4(1.0, 1.0, 1.0, 1.0);
	vUV = vertexUV;

	if (bShowVertexColor)
	{
		vColor.rgb = vertexColors;
	}

	if (bShowVertexAlpha)
	{
		vColor.a = vertexAlpha;
	}

	// Eye-coordinate position of vertex
	vec3 vPos = vec3(matModelView * vec4(vertexPosition, 1.0));
	gl_Position = matProjection * vec4(vPos, 1.0);

	vec3 mv_normal = mv_normalMatrix * vertexNormal;
	vec3 mv_tangent = mv_normalMatrix * vertexTangent;
	vec3 mv_bitangent = mv_normalMatrix * vertexBitangent;

    mv_tbn = mat3(mv_tangent.x,   mv_tangent.y,   mv_tangent.z,
              mv_bitangent.x, mv_bitangent.y, mv_bitangent.z,
              mv_normal.x,    mv_normal.y,    mv_normal.z);

	viewDir = normalize(-vPos);
	lightFrontal = normalize(mat3(matView) * frontal.direction);
	lightDirectional0 = normalize(mat3(matView) * directional0.direction);
	lightDirectional1 = normalize(mat3(matView) * directional1.direction);
	lightDirectional2 = normalize(mat3(matView) * directional2.direction);

	if (!bShowTexture || bWireframe)
	{
		vColor *= clamp(vec4(color, 1.0), 0.0, 1.0);
	}

	if (!bWireframe)
	{
		vColor.rgb *= subColor;

		if (bShowWeight)
		{
			weightColor = colorRamp(vertexWeight);
		}
	}
}
"

    Private ReadOnly fragmentShaderSource As String = "
#version 440

/*
 * BodySlide and Outfit Studio
 * Shaders by jonwd7 and ousnius
 * https://github.com/ousnius/BodySlide-and-Outfit-Studio
 * http://www.niftools.org/
 */

uniform sampler2D texDiffuse;
uniform sampler2D texNormal;
uniform samplerCube texCubemap;
uniform sampler2D texEnvMask;
uniform sampler2D texSpecular;
uniform sampler2D texGreyscale;
uniform sampler2D texGlowmap;

uniform bool bLightEnabled;
uniform bool bShowTexture;
uniform bool bShowMask;
uniform bool bShowWeight;
uniform bool bWireframe;
uniform bool bApplyZap;

uniform bool bNormalMap;
uniform bool bModelSpace;
uniform bool bCubemap;
uniform bool bEnvMask;
uniform bool bSpecular;
uniform bool bEmissive;
uniform bool bBacklight;
uniform bool bRimlight;
uniform bool bSoftlight;
uniform bool bAlphaTest;
uniform bool bGlowmap;
uniform bool bGreyscaleColor;
uniform bool bHide;

uniform mat4 matModel;
uniform mat4 matModelViewInverse;
uniform float DebugMode;

uniform	vec2 uvOffset;
uniform vec2 uvScale;
uniform	vec3 specularColor;
uniform	float specularStrength;
uniform	float shininess;
uniform float envReflection;
uniform vec3 emissiveColor;
uniform float emissiveMultiple;
uniform float alpha;
uniform float backlightPower;
uniform float rimlightPower;
uniform	float subsurfaceRolloff;
uniform	float fresnelPower;
uniform float paletteScale;
uniform float WireAlpha;

uniform float alphaThreshold;

uniform float ambient;

struct DirectionalLight
{
	vec3 diffuse;
	vec3 direction;
};

uniform DirectionalLight frontal;
uniform DirectionalLight directional0;
uniform DirectionalLight directional1;
uniform DirectionalLight directional2;

in vec3 lightFrontal;
in vec3 lightDirectional0;
in vec3 lightDirectional1;
in vec3 lightDirectional2;

in vec3 viewDir;
in mat3 mv_tbn;

in float maskFactor;
flat in int ZappedVert;
in vec3 weightColor;

in vec4 vColor;
in vec2 vUV;

out vec4 fragColor;

vec3 normal = vec3(0.0);
float specGloss = 1.0;
float specFactor = 1.0;

vec2 uv = vec2(0.0);
vec3 albedo = vec3(0.0);
vec3 emissive = vec3(0.0);

vec4 baseMap = vec4(0.0);
vec4 normalMap = vec4(0.0);
vec4 specMap = vec4(0.0);
vec4 envMask = vec4(0.0);

#ifndef M_PI
	#define M_PI 3.1415926535897932384626433832795
#endif

#define FLT_EPSILON 1.192092896e-07F // smallest such that 1.0 + FLT_EPSILON != 1.0

float OrenNayarFull(vec3 L, vec3 V, vec3 N, float roughness, float NdotL)
{
	//float NdotL = dot(N, L);
	float NdotV = dot(N, V);
	float LdotV = dot(L, V);

	float angleVN = acos(max(NdotV, FLT_EPSILON));
	float angleLN = acos(max(NdotL, FLT_EPSILON));

	float alpha = max(angleVN, angleLN);
	float beta = min(angleVN, angleLN);
	float gamma = LdotV - NdotL * NdotV;

	float roughnessSquared = roughness * roughness;
	float roughnessSquared9 = (roughnessSquared / (roughnessSquared + 0.09));

	// C1, C2, and C3
	float C1 = 1.0 - 0.5 * (roughnessSquared / (roughnessSquared + 0.33));
	float C2 = 0.45 * roughnessSquared9;

	if( gamma >= 0.0 )
		C2 *= sin(alpha);
	else
		C2 *= (sin(alpha) - pow((2.0 * beta) / M_PI, 3.0));

	float powValue = (4.0 * alpha * beta) / (M_PI * M_PI);
	float C3 = 0.125 * roughnessSquared9 * powValue * powValue;

	// Avoid asymptote at pi/2
	float asym = M_PI / 2.0;
	float lim1 = asym + 0.01;
	float lim2 = asym - 0.01;

	float ab2 = (alpha + beta) / 2.0;

	if (beta >= asym && beta < lim1)
		beta = lim1;
	else if (beta < asym && beta >= lim2)
		beta = lim2;

	if (ab2 >= asym && ab2 < lim1)
		ab2 = lim1;
	else if (ab2 < asym && ab2 >= lim2)
		ab2 = lim2;

	// Reflection
	float A = gamma * C2 * tan(beta);
	float B = (1.0 - abs(gamma)) * C3 * tan(ab2);

	float L1 = max(FLT_EPSILON, NdotL) * (C1 + A + B);

	// Interreflection
	float twoBetaPi = 2.0 * beta / M_PI;
	float L2 = 0.17 * max(FLT_EPSILON, NdotL) * (roughnessSquared / (roughnessSquared + 0.13)) * (1.0 - gamma * twoBetaPi * twoBetaPi);

	return L1 + L2;
}

// Schlick's Fresnel approximation
float fresnelSchlick(float VdotH, float F0)
{
	float base = 1.0 - VdotH;
	float exp = pow(base, fresnelPower);
	return clamp(exp + F0 * (1.0 - exp), 0.0, 1.0);
}

// The Torrance-Sparrow visibility factor, G
float VisibDiv(float NdotL, float NdotV, float VdotH, float NdotH)
{
	float denom = max(VdotH, FLT_EPSILON);
	float numL = min(NdotV, NdotL);
	float numR = 2.0 * NdotH;
	if (denom >= (numL * numR))
	{
		numL = (numL == NdotV) ? 1.0 : (NdotL / NdotV);
		return (numL * numR) / denom;
	}
	return 1.0 / NdotV;
}

// this is a normalized Phong model used in the Torrance-Sparrow model
vec3 TorranceSparrow(float NdotL, float NdotH, float NdotV, float VdotH, vec3 color, float power, float F0)
{
	// D: Normalized phong model
	float D = ((power + 2.0) / (2.0 * M_PI)) * pow(NdotH, power);

	// G: Torrance-Sparrow visibility term divided by NdotV
	float G_NdotV = VisibDiv(NdotL, NdotV, VdotH, NdotH);

	// F: Schlick's approximation
	float F = fresnelSchlick(VdotH, F0);

	// Torrance-Sparrow:
	// (F * G * D) / (4 * NdotL * NdotV)
	// Division by NdotV is done in VisibDiv()
	// and division by NdotL is removed since
	// outgoing radiance is determined by:
	// BRDF * NdotL * L()
	float spec = (F * G_NdotV * D) / 4.0;

	return color * spec * M_PI;
}

vec3 tonemap(in vec3 x)
{
	const float A = 0.15;
	const float B = 0.50;
	const float C = 0.10;
	const float D = 0.20;
	const float E = 0.02;
	const float F = 0.30;

	return ((x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * F)) - E / F;
}

void directionalLight(in DirectionalLight light, in vec3 lightDir, inout vec3 outDiffuse, inout vec3 outSpec)
{
	vec3 halfDir = normalize(lightDir + viewDir);
	float NdotL = dot(normal, lightDir);
	float NdotL0 = max(NdotL, FLT_EPSILON);
	float NdotH = max(dot(normal, halfDir), FLT_EPSILON);
	float NdotV = max(dot(normal, viewDir), FLT_EPSILON);
	float VdotH = max(dot(viewDir, halfDir), FLT_EPSILON);

	// Temporary diffuse
	vec3 diff = ambient + NdotL0 * light.diffuse;

	// Specularity
	float smoothness = 1.0;
	float roughness = 0.0;
	float specMask = 1.0;
	if (bSpecular && bShowTexture)
	{
		smoothness = specGloss * shininess;
		roughness = 1.0 - smoothness;
		float fSpecularPower = exp2(smoothness * 10.0 + 1.0);
		specMask = specFactor * specularStrength;

		outSpec += TorranceSparrow(NdotL0, NdotH, NdotV, VdotH, vec3(specMask), fSpecularPower, 0.2) * NdotL0 * light.diffuse * specularColor;
		outSpec += ambient * specMask * fresnelSchlick(VdotH, 0.2) * (1.0 - NdotV) * light.diffuse;
	}

	// Environment
	if (bCubemap && bShowTexture)
	{
		vec3 reflected = reflect(viewDir, normal);
		vec3 reflectedWS = vec3(matModel * (matModelViewInverse * vec4(reflected, 0.0)));

		vec4 cube = textureLod(texCubemap, reflectedWS, 8.0 - smoothness * 8.0);
		cube.rgb *= envReflection * specularStrength;
		if (bEnvMask)
		{
			cube.rgb *= envMask.r;
		}
		else
		{
			// No env mask, use specular factor
			cube.rgb *= specFactor;
		}

		outSpec += cube.rgb * diff;
	}

	// Back lighting not really useful for the current light setup of multiple directional lights
	//if (bBacklight)
	//{
	//	float NdotNegL = max(dot(normal, -lightDir), FLT_EPSILON);
	//	vec3 backlight = albedo * NdotNegL * clamp(backlightPower, 0.0, 1.0);
	//	emissive += backlight * light.diffuse;
	//}

	// Rim lighting not really useful for the current light setup of multiple directional lights
	//if (bRimlight)
	//{
	//	vec3 rim = vec3(pow((1.0 - NdotV), rimlightPower));
	//	rim *= smoothstep(-0.2, 1.0, dot(-lightDir, viewDir));
	//	emissive += rim * light.diffuse * specMask;
	//}

	// Diffuse
	diff = vec3(OrenNayarFull(lightDir, viewDir, normal, roughness, NdotL0));
	outDiffuse += diff * light.diffuse;

	// Soft Lighting
	if (bSoftlight)
	{
		float wrap = (NdotL + subsurfaceRolloff) / (1.0 + subsurfaceRolloff);
		vec3 soft = albedo * max(0.0, wrap) * smoothstep(1.0, 0.0, sqrt(diff));
		outDiffuse += soft;
	}
}

vec4 colorLookup(in float x, in float y)
{
	return texture(texGreyscale, vec2(clamp(x, 0.0, 1.0), clamp(y, 0.0, 1.0)));
}

void main(void)
{
    uv = vUV * uvScale + uvOffset;
	vec4 color = vColor;
	albedo = vColor.rgb;

	if (!bWireframe)
	{
		if (bShowTexture)
		{
			// Diffuse Texture
			baseMap = texture(texDiffuse, uv);
			albedo *= baseMap.rgb;
			color.a *= baseMap.a;

			// Diffuse texture without lighting
			color.rgb = albedo;

			if (bLightEnabled)
			{
				if (bNormalMap)
				{
					normalMap = texture(texNormal, uv);
                
					if (bSpecular)
					{
						// Specular Map
						specMap = texture(texSpecular, uv);
						specGloss = specMap.g;
						specFactor = specMap.r;
					}
				}

				if (bCubemap)
				{
					if (bEnvMask)
					{
						// Environment Mask
						envMask = texture(texEnvMask, uv);
					}
				}
			}
		}

		if (bLightEnabled)
		{
			// Lighting with or without textures
			vec3 outDiffuse = vec3(0.0);
			vec3 outSpecular = vec3(0.0);

			// Start off neutral
			normal = normalize(mv_tbn * vec3(0.0, 0.0, 0.5));

			if (bShowTexture)
			{
				if (bNormalMap)
				{
					if (bModelSpace)
					{
						// No proper FO4 model space map rendering yet
						//normal = normalize(normalMap.rgb * 2.0 - 1.0);
						//normal.r = -normal.r;
					}
					else
					{
						normal = (normalMap.rgb * 2.0 - 1.0);

						// Calculate missing blue channel
						normal.b = sqrt(1.0 - dot(normal.rg, normal.rg));

						// Tangent space map
						normal = normalize(mv_tbn * normal);
					}
				}

				if (bGreyscaleColor)
				{
                    float avg =(baseMap.r + baseMap.g + baseMap.b) / 3.0;
                    vec4 luG = colorLookup(avg, paletteScale);
					albedo = luG.rgb;
				}
			}

			directionalLight(frontal, lightFrontal, outDiffuse, outSpecular);
			directionalLight(directional0, lightDirectional0, outDiffuse, outSpecular);
			directionalLight(directional1, lightDirectional1, outDiffuse, outSpecular);
			directionalLight(directional2, lightDirectional2, outDiffuse, outSpecular);

			// Emissive
			if (bEmissive)
			{
				emissive += emissiveColor * emissiveMultiple;

				// Glowmap
				if (bGlowmap)
				{
					vec4 glowMap = texture(texGlowmap, uv);
					emissive *= glowMap.rgb;
				}
			}

			color.rgb = outDiffuse * albedo;
			color.rgb += outSpecular;
			color.rgb += emissive;
			color.rgb += ambient * albedo;
		}

		if (bShowMask)
		{
          color.rgb *= maskFactor;
		}

		if (bShowWeight)
		{
			color.rgb *= weightColor;
		}

		color.rgb = tonemap(color.rgb) / tonemap(vec3(1.0));
	}
	else
	{
    vec3 shaded = color.rgb ;  
     if (bShowTexture)
     {
     shaded=texture(texDiffuse, uv).rgb;
      }
     shaded *= maskFactor;
     color = vec4(shaded, WireAlpha) ;
	}

	color = clamp(color, 0.0, 1.0);

	fragColor = color;



//====================DEBUG MODE==========================
if (DebugMode > 0.0) {
    // Calculamos en view-space las tres direcciones TBN
    vec3 dbgTangent  = normalize(mv_tbn * vec3(1.0, 0.0, 0.0));
    vec3 dbgBitangent= normalize(mv_tbn * vec3(0.0, 1.0, 0.0));
    vec3 dbgNormal   = normalize(mv_tbn * vec3(0.0, 0.0, 1.0));


    // Mapeo de –1..1 a 0..1 para visualizar en color
    dbgNormal    = dbgNormal    * 0.5 + 0.5;
    dbgTangent   = dbgTangent   * 0.5 + 0.5;
    dbgBitangent = dbgBitangent * 0.5 + 0.5;

    if (abs(DebugMode - 1.0) < 0.5) {
        // Modo 1: normales
        fragColor = vec4(dbgNormal, 1.0);
    }
    else if (abs(DebugMode - 2.0) < 0.5) {
        // Modo 2: tangentes
        fragColor = vec4(dbgTangent, 1.0);
    }
    else if (abs(DebugMode - 3.0) < 0.5) {
        // Modo 3: bitangentes
        fragColor = vec4(dbgBitangent, 1.0);
    }
else if (abs(DebugMode - 4.0) < 0.5) {
    vec3 Tm = normalize(mv_tbn * vec3(1.0, 0.0, 0.0));
    vec3 Bm = normalize(mv_tbn * vec3(0.0, 1.0, 0.0));
    vec3 Nm = normalize(mv_tbn * vec3(0.0, 0.0, 1.0));

    vec3 Tgs = normalize(Tm - Nm * dot(Nm, Tm));
    vec3 Bx  = normalize(cross(Nm, Tgs));
    float h  = sign(dot(Bm, Bx));
    mat3 tbn_fixed = mat3(Tgs, Bx * h, Nm);

    vec3 n_ts = vec3(0.0, 0.0, 1.0);
    if (bShowTexture && bNormalMap && !bModelSpace) {
        vec3 nm = texture(texNormal, uv).rgb * 2.0 - 1.0;
        nm.z = sqrt(max(FLT_EPSILON, 1.0 - dot(nm.xy, nm.xy)));
        n_ts = nm;
    }

    vec3 nA = normalize(mv_tbn   * n_ts);
    vec3 nB = normalize(tbn_fixed * n_ts);

    float errN = 0.5 * length(nA - nB);

    float IA = max(dot(nA, lightFrontal), 0.0)
             + max(dot(nA, lightDirectional0), 0.0)
             + max(dot(nA, lightDirectional1), 0.0)
             + max(dot(nA, lightDirectional2), 0.0);

    float IB = max(dot(nB, lightFrontal), 0.0)
             + max(dot(nB, lightDirectional0), 0.0)
             + max(dot(nB, lightDirectional1), 0.0)
             + max(dot(nB, lightDirectional2), 0.0);

    float errL = abs(IA - IB);

    float E = clamp(max(errN, errL), 0.0, 1.0);

    float good = 1.0 - smoothstep(0.0, 0.15, E);
    float bad  = smoothstep(0.0, 0.15, E);
    float hvis = h * 0.5 + 0.5;

    fragColor = vec4(bad, good, hvis, 1.0);
    return;
}
}
//===================END DEBUG MODE=======================

if (bHide)
	    {
            discard;
	    }

  	if (bApplyZap) // Codigo Manolo para el ZAP
    {
  //  if (!bShowMask)
   // {
  	    if (ZappedVert==1)
	    {
    	    discard;
	    }
        }
    //}

   	if (!bWireframe)
	{
		fragColor.a *= alpha;

		if (bAlphaTest)
			if (fragColor.a <= alphaThreshold) // GL_GREATER
				discard;

	}

}
"



    Private program As Integer
    ' Método público para liberar recursos.
    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(disposing:=True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
        If Not disposedValue Then
            If program > 0 And disposing Then
                GL.DeleteProgram(program)
                program = 0
            End If
        End If
        disposedValue = True
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(disposing:=False)
        MyBase.Finalize()
    End Sub

    Public Sub New()
        Dim vertexShader = CompileShader(ShaderType.VertexShader, vertexShaderSource)
        Dim fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentShaderSource)

        program = GL.CreateProgram()
        GL.AttachShader(program, vertexShader)
        GL.AttachShader(program, fragmentShader)
        GL.LinkProgram(program)

        GL.DetachShader(program, vertexShader)
        GL.DetachShader(program, fragmentShader)
        GL.DeleteShader(vertexShader)
        GL.DeleteShader(fragmentShader)
    End Sub

    Private Shared Function CompileShader(type As ShaderType, source As String) As Integer
        Dim shader = GL.CreateShader(type)
        GL.ShaderSource(shader, source)
        GL.CompileShader(shader)
        Dim info = GL.GetShaderInfoLog(shader)
        If Not String.IsNullOrWhiteSpace(info) Then Throw New Exception($"Error compiling {type}: {info}")
        Return shader
    End Function

    Public Sub Use()
        GL.UseProgram(program)
    End Sub
    Public Debugmode As Integer = 0
    Public Shared Function Color_to_Vector(color As Color) As Vector3
        Return New Vector3(color.R / 255.0F, color.G / 255.0F, color.B / 255.0F)
    End Function
    Public Sub SetFloat(name As String, value As Single)
        Dim loc As Integer = GL.GetUniformLocation(program, name)
        If loc <> -1 Then
            GL.Uniform1(loc, value)
            '#If DEBUG Then
            '            Debug.Print($"[Uniform] {name} = {value}")
            '#End If
        Else
            '#If DEBUG Then
            '            Debug.Print($"⚠️ [Uniform] {name} not found!")
            '#End If
        End If
    End Sub

    Public Sub SetInt(name As String, value As Integer)
        Dim loc As Integer = GL.GetUniformLocation(program, name)
        If loc <> -1 Then
            GL.Uniform1(loc, value)
            '#If DEBUG Then
            '            Debug.Print($"[Uniform] {name} = {value}")
            '#End If
        Else
            '#If DEBUG Then
            '            Debug.Print($"⚠️ [Uniform] {name} not found!")
            '#End If
        End If
    End Sub

    Public Sub SetBool(name As String, value As Boolean)
        SetInt(name, If(value, 1, 0))
    End Sub

    Public Sub SetVector2(name As String, value As Vector2)
        Dim loc As Integer = GL.GetUniformLocation(program, name)
        If loc <> -1 Then
            GL.Uniform2(loc, value.X, value.Y)
            '#If DEBUG Then
            '            Debug.Print($"[Uniform] {name} = {value}")
            '#End If
        Else
            '#If DEBUG Then
            '            Debug.Print($"⚠️ [Uniform] {name} not found!")
            '#End If
        End If
    End Sub

    Public Sub SetVector3(name As String, value As Vector3)
        Dim loc As Integer = GL.GetUniformLocation(program, name)
        If loc <> -1 Then
            GL.Uniform3(loc, value.X, value.Y, value.Z)
            '#If DEBUG Then
            '            Debug.Print($"[Uniform] {name} = {value}")
            '#End If
        Else
            '#If DEBUG Then
            '            Debug.Print($"⚠️ [Uniform] {name} not found!")
            '#End If
        End If
    End Sub

    Public Sub SetMatrix3(name As String, value As Matrix3)
        Dim loc As Integer = GL.GetUniformLocation(program, name)
        If loc <> -1 Then
            GL.UniformMatrix3(loc, False, value)
            '#If DEBUG Then
            '            Debug.Print($"[Uniform] {name} = {value}")
            '#End If
        Else
            '#If DEBUG Then
            '            Debug.Print($"⚠️ [Uniform] {name} not found!")
            '#End If
        End If
    End Sub

    Public Sub SetMatrix4(name As String, value As Matrix4)
        Dim loc As Integer = GL.GetUniformLocation(program, name)
        If loc <> -1 Then
            GL.UniformMatrix4(loc, False, value)
            '#If DEBUG Then
            '            Debug.Print($"[Uniform] {name} = {value}")
            '#End If
        Else
            '#If DEBUG Then
            '            Debug.Print($"⚠️ [Uniform] {name} not found!")
            '#End If
        End If
    End Sub

    Public Sub BindTexture(uniformName As String, textureID As Integer, unit As TextureUnit)
        GL.ActiveTexture(unit)
        GL.BindTexture(TextureTarget.Texture2D, textureID)
        SetInt(uniformName, unit - TextureUnit.Texture0)
        '#If DEBUG Then
        '        Debug.Print($"[Texture] {uniformName} bound to TextureUnit {unit - TextureUnit.Texture0} with ID {textureID}")
        '#End If
    End Sub

    Public Sub BindCubeMap(uniformName As String, textureID As Integer, unit As TextureUnit)
        GL.ActiveTexture(unit)
        GL.BindTexture(TextureTarget.TextureCubeMap, textureID)
        SetInt(uniformName, unit - TextureUnit.Texture0)
        '#If DEBUG Then
        '        Debug.Print($"[CubeMap] {uniformName} bound to TextureUnit {unit - TextureUnit.Texture0} with ID {textureID}")
        '#End If
    End Sub
End Class

