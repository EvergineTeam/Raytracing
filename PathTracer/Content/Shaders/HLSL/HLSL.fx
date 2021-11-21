cbuffer WorldInfo : register(b0)
{
	float3 CameraPosition : packoffset(c0.x);
	uint NumBounces : packoffset(c0.w);
	float4 LightAmbientColor : packoffset(c1.x);
	float3 LightPosition : packoffset(c2.x);
	uint NumRays : packoffset(c2.w);
	float4 LightDiffuseColor : packoffset(c3.x);
	float4 LightSpecularColor : packoffset(c4.x);
	float DiffuseCoef : packoffset(c5.x);
	float SpecularCoef : packoffset(c5.y);
	float SpecularPower : packoffset(c5.z);
	float InShadowRadiance : packoffset(c5.w);
	uint FrameCount : packoffset(c6.x);
	float LightRadius : packoffset(c6.y);
	int PathTracerSampleIndex : packoffset(c6.z);
	float PathTracerAccumulationFactor : packoffset(c6.w);
	float AORadius : packoffset(c7.x);
	float AORayMin : packoffset(c7.y);
	float2 PixelOffset : packoffset(c7.z);
	float ReflectanceCoef : packoffset(c8.x);
	uint MaxRecursionDepth : packoffset(c8.y);
	float4x4 CameraWorldViewProj : packoffset(c9.x);
};

RaytracingAccelerationStructure gRtScene : register(t0);
RWTexture2D<float4> gOutput : register(u0);


StructuredBuffer<int> Indices : register(t1);
StructuredBuffer<float3> Normals : register(t2);
StructuredBuffer<float2> Texcoords : register(t3);

Texture2D DiffuseTexture : register(t4);
Texture2D RoughnessTexture : register(t5);
SamplerState DiffuseSampler : register(s0);

/* Helpers */

float4 linearToSrgb(float4 c)
{
	// Based on http://chilliant.blogspot.com/2012/08/srgb-approximations-for-hlsl.html
	float4 sq1 = sqrt(c);
	float4 sq2 = sqrt(sq1);
	float4 sq3 = sqrt(sq2);
	float4 srgb = 0.662002687 * sq1 + 0.684122060 * sq2 - 0.323583601 * sq3 - 0.0225411470 * c;
	return srgb;
}

// Retrieve hit world position.
float3 HitWorldPosition()
{
	return WorldRayOrigin() + RayTCurrent() * WorldRayDirection();
}

// Generate a ray in world space for a camera pixel corresponding to an index from the dispatched 2D grid.
inline void GenerateCameraRay(uint2 index, out float3 origin, out float3 direction)
{
	float2 xy = index + PixelOffset;
	float2 screenPos = xy / DispatchRaysDimensions().xy * 2.0 - 1.0;

	// Invert Y for DirectX-style coordinates.
	screenPos.y = -screenPos.y;

	// Unproject the pixel coordinate into a ray.
	float4 world = mul(float4(screenPos, 0, 1), CameraWorldViewProj);

	world.xyz /= world.w;
	origin = CameraPosition;
	direction = normalize(world.xyz - origin);
}

// Diffuse lighting calculation.
float CalculateDiffuseCoefficient(in float3 hitPosition, in float3 incidentLightRay, in float3 normal)
{
	float fNDotL = saturate(dot(-incidentLightRay, normal));
	return fNDotL;
}

// Phong lighting specular component
float4 CalculateSpecularCoefficient(in float3 hitPosition, in float3 incidentLightRay, in float3 normal, in float specularPower)
{
	float3 reflectedLightRay = normalize(reflect(incidentLightRay, normal));
	return pow(saturate(dot(reflectedLightRay, normalize(-WorldRayDirection()))), specularPower);
}

// Phong lighting model = ambient + diffuse + specular components.
float4 CalculatePhongLighting(in float4 albedo, in float3 normal, in bool isInShadow, in float diffuseCoef, in float specularCoef, in float specularPower)
{
	float3 hitPosition = HitWorldPosition();
	float shadowFactor = isInShadow ? InShadowRadiance : 1.0;
	float3 incidentLightRay = normalize(hitPosition - LightPosition);

	// Diffuse component.
	float Kd = CalculateDiffuseCoefficient(hitPosition, incidentLightRay, normal);
	float4 diffuseColor = shadowFactor * diffuseCoef * Kd * LightDiffuseColor * albedo;

	// Specular component.
	float4 specularColor = float4(0, 0, 0, 0);
	if (!isInShadow)
	{
		float4 lightSpecularColor = float4(1, 1, 1, 1);
		float4 Ks = CalculateSpecularCoefficient(hitPosition, incidentLightRay, normal, specularPower);
		specularColor = specularCoef * Ks * LightSpecularColor;
	}

	// Ambient component.
	// Fake AO: Darken faces with normal facing downwards/away from the sky a little bit.
	float4 ambientColor = LightAmbientColor;
	float4 ambientColorMin = LightAmbientColor - 0.1;
	float4 ambientColorMax = LightAmbientColor;
	float a = 1 - saturate(dot(normal, float3(0, -1, 0)));
	ambientColor = albedo * lerp(ambientColorMin, ambientColorMax, a);

	return ambientColor + diffuseColor + specularColor;
}

// Fresnel reflectance - schlick approximation.
float3 FresnelReflectanceSchlick(in float3 I, in float3 N, in float3 f0)
{
	float cosi = saturate(dot(-I, N));
	return f0 + (1 - f0) * pow(1 - cosi, 5);
}

struct Ray
{
	float3 origin;
	float3 direction;
};

struct RayPayload
{
	float4 color;
	uint recursionDepth;
};

struct ShadowRayPayload
{
	bool hit;
};

struct AORayPayload
{
	float aoValue;  // Store 0 if we hit a surface, 1 if we miss all surfaces
};

struct GIRayPayload
{
	float3 color;
	uint depth;
	uint seed;
};

float4 TraceRadianceRay(in Ray ray, in uint currentRayRecursionDepth)
{
	if (currentRayRecursionDepth >= MaxRecursionDepth)
	{
		return float4(0, 0, 0, 0);
	}

	// Set the ray's extents.
	RayDesc rayDesc;
	rayDesc.Origin = ray.origin;
	rayDesc.Direction = ray.direction;
	// Set TMin to a zero value to avoid aliasing artifacts along contact areas.
	// Note: make sure to enable face culling so as to avoid surface face fighting.
	rayDesc.TMin = 0.01;
	rayDesc.TMax = 10000;
	RayPayload rayPayload = { float4(0, 0, 0, 0), currentRayRecursionDepth + 1 };
	TraceRay(gRtScene,
		RAY_FLAG_CULL_BACK_FACING_TRIANGLES,
		0xFF,
		0,
		0,
		0,
		rayDesc, rayPayload);

	return rayPayload.color;
}

bool TraceShadowRayAndReportIfHit(in Ray ray)
{
	// Set the ray's extents.
	RayDesc rayDesc;
	rayDesc.Origin = ray.origin;
	rayDesc.Direction = ray.direction;
	// Set TMin to a zero value to avoid aliasing artifcats along contact areas.
	// Note: make sure to enable back-face culling so as to avoid surface face fighting.
	rayDesc.TMin = 0.01;
	rayDesc.TMax = 10000;

	// Initialize shadow ray payload.
	// Set the initial value to true since closest and any hit shaders are skipped. 
	// Shadow miss shader, if called, will set it to false.
	ShadowRayPayload shadowPayload = { true };
	TraceRay(gRtScene,		
		RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH,
		0xFF,
		1,
		0,
		1,
		rayDesc, shadowPayload);

	return shadowPayload.hit;
}

// A wrapper function that encapsulates shooting an ambient occlusion ray query
float TraceAORay(float3 orig, float3 dir, float minT, float maxT)
{
	// Setup ambient occlusion ray and payload
	AORayPayload  rayPayload = { 0.0f };  // Specified value is returned if we hit a surface
	RayDesc       rayAO;
	rayAO.Origin = orig;               // Where does our ray start?
	rayAO.Direction = dir;                // What direction does our ray go?
	rayAO.TMin = minT;               // Min distance to detect an intersection
	rayAO.TMax = maxT;               // Max distance to detect an intersection

	// Trace our ray.  Ray stops after it's first definite hit; never execute closest hit shader
	TraceRay(gRtScene,
		RAY_FLAG_NONE,
		0xFF,
		2,
		0,
		2,
		rayAO,
		rayPayload);

	// Copy our AO value out of the ray payload.
	return rayPayload.aoValue;
}

float3 TraceGIRay(float3 orig, float3 dir, in uint randSeed, in uint depth)
{
	// Set the ray's extents.
	RayDesc rayDesc;
	rayDesc.Origin = orig;
	rayDesc.Direction = dir;
	// Set TMin to a zero value to avoid aliasing artifacts along contact areas.
	// Note: make sure to enable face culling so as to avoid surface face fighting.
	rayDesc.TMin = 0.001;
	rayDesc.TMax = 10000;

	GIRayPayload rayPayload;
	rayPayload.color = float3(0, 0, 0);
	rayPayload.depth = depth + 1;
	rayPayload.seed = randSeed;

	TraceRay(gRtScene,
		RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH,
		0xFF,
		3,
		0,
		3,
		rayDesc,
		rayPayload);

	return rayPayload.color;
}

[shader("raygeneration")]
void rayGen()
{
	uint2 launchIndex = DispatchRaysIndex().xy;

	// Generate a ray for a camera pixel corresponding to an index from the dispatched 2D grid.
	float3 origin;
	float3 rayDir;
	GenerateCameraRay(launchIndex, origin, rayDir);

	// Trace the ray.	
	Ray ray;
	ray.origin = origin;
	ray.direction = rayDir;
	float4 color = TraceRadianceRay(ray, 0);

	// Path tracer
	if (PathTracerSampleIndex == 0)
	{
		gOutput[launchIndex] = 0;
	}

	gOutput[launchIndex] = lerp(gOutput[launchIndex], float4(color.xyz, 1), PathTracerAccumulationFactor);
}

static float4 backgroundColor = float4(0.09, 0.08, 0.14, 1.0);
[shader("miss")]
void miss(inout RayPayload payload)
{
	payload.color = backgroundColor;
}

[shader("miss")]
void missShadow(inout ShadowRayPayload payload)
{
	payload.hit = false;
}

[shader("miss")]
void AoMiss(inout AORayPayload payload)
{
	// Our ambient occlusion value is 1 if we hit nothing.
	payload.aoValue = 1.0f;
}

[shader("miss")]
void GIMiss(inout GIRayPayload payload)
{
	payload.color = backgroundColor.xyz;
}

uint3 LoadIndices(StructuredBuffer<int> Indices, uint offset)
{
	uint3 indices;

	float index = offset / 2;
	const int2 values = uint2(Indices[index], Indices[index + 1]);

	if (offset % 2 == 0)
	{
		indices.x = values.x & 0xffff;
		indices.y = (values.x >> 16) & 0xffff;
		indices.z = values.y & 0xffff;
	}
	else
	{
		indices.x = (values.x >> 16) & 0xffff;
		indices.y = values.y & 0xffff;
		indices.z = (values.y >> 16) & 0xffff;
	}

	return indices;
}

// Calculates barycentrical interpolation factors based on actual barycentrics
float3 CalculateBarycentricalInterpolationFactors(in float2 barycentrics)
{
	return float3(1.0 - barycentrics.x - barycentrics.y, barycentrics.x, barycentrics.y);
}

// b == output from CalculateBarycentricalInterpolationFactors()
float2 BarycentricInterpolation(in float2 a0, in float2 a1, in float2 a2, in float3 b) {
	return b.x * a0 + b.y * a1 + b.z * a2;
}

// b == output from CalculateBarycentricalInterpolationFactors()
float3 BarycentricInterpolation(in float3 a0, in float3 a1, in float3 a2, in float3 b) {
	return b.x * a0 + b.y * a1 + b.z * a2;
}

// Generates a seed for a random number generator from 2 inputs plus a backoff
uint initRand(uint val0, uint val1, uint backoff = 16)
{
	uint v0 = val0, v1 = val1, s0 = 0;

	[unroll]
	for (uint n = 0; n < backoff; n++)
	{
		s0 += 0x9e3779b9;
		v0 += ((v1 << 4) + 0xa341316c) ^ (v1 + s0) ^ ((v1 >> 5) + 0xc8013ea4);
		v1 += ((v0 << 4) + 0xad90777d) ^ (v0 + s0) ^ ((v0 >> 5) + 0x7e95761e);
	}
	return v0;
}

// Takes our seed, updates it, and returns a pseudorandom float in [0..1]
float nextRand(inout uint s)
{
	s = (1664525u * s + 1013904223u);
	return float(s & 0x00FFFFFF) / float(0x01000000);
}

// Utility function to get a vector perpendicular to an input vector 
//    (from "Efficient Construction of Perpendicular Vectors Without Branching")
float3 getPerpendicularVector(float3 u)
{
	float3 a = abs(u);
	uint xm = ((a.x - a.y) < 0 && (a.x - a.z) < 0) ? 1 : 0;
	uint ym = (a.y - a.z) < 0 ? (1 ^ xm) : 0;
	uint zm = 1 ^ (xm | ym);
	return cross(u, float3(xm, ym, zm));
}

// Rotation with angle (in radians) and axis
float3x3 angleAxis3x3(float angle, float3 axis) {
	float c, s;
	sincos(angle, s, c);

	float t = 1 - c;
	float x = axis.x;
	float y = axis.y;
	float z = axis.z;

	return float3x3(
		t * x * x + c, t * x * y - s * z, t * x * z + s * y,
		t * x * y + s * z, t * y * y + c, t * y * z - s * x,
		t * x * z - s * y, t * y * z + s * x, t * z * z + c
		);
}

float3 getConeSample(inout uint randSeed, float3 shadePosition, float3 lightPosition)
{
	float3 toLight = normalize(lightPosition - shadePosition);

	float3 perpL = getPerpendicularVector(toLight);

	float3 toLightEdge = normalize((lightPosition + perpL * LightRadius) - shadePosition);

	float coneAngle = LightRadius > 0 ? acos(dot(toLight, toLightEdge)) * 2.0 : 0.0;

	float cosAngle = cos(coneAngle);

	float2 randVal = float2(nextRand(randSeed), nextRand(randSeed));

	float z = randVal.x * (1.0f - cosAngle) + cosAngle;
	float phi = randVal.y * 2.0f * 3.14159265f;

	float x = sqrt(1.0f - z * z) * cos(phi);
	float y = sqrt(1.0f - z * z) * sin(phi);
	float3 north = float3(0.f, 0.f, 1.f);

	// Find the rotation axis `u` and rotation angle `rot` [1]
	float3 axis = normalize(cross(north, toLight));
	float angle = acos(dot(toLight, north));

	// Convert rotation axis and angle to 3x3 rotation matrix [2]
	float3x3 R = angleAxis3x3(angle, axis);

	return mul(R, float3(x, y, z));
}

// Get a cosine-weighted random vector centered around a specified normal direction.
float3 getCosHemisphereSample(inout uint randSeed, float3 hitNorm)
{
	// Get 2 random numbers to select our sample with
	float2 randVal = float2(nextRand(randSeed), nextRand(randSeed));

	// Cosine weighted hemisphere sample from RNG
	float3 bitangent = getPerpendicularVector(hitNorm);
	float3 tangent = cross(bitangent, hitNorm);
	float r = sqrt(randVal.x);
	float phi = 2.0f * 3.14159265f * randVal.y;

	// Get our cosine-weighted hemisphere lobe sample direction
	return tangent * (r * cos(phi).x) + bitangent * (r * sin(phi)) + hitNorm.xyz * sqrt(1 - randVal.x);
}

[shader("closesthit")]
void chs(inout RayPayload payload, in BuiltInTriangleIntersectionAttributes attribs)
{
	float3 hitPosition = WorldRayOrigin() + RayTCurrent() * WorldRayDirection();

	// Get the base index of the triangle's    
	uint indicesPerTriangle = 3;
	uint baseIndex = PrimitiveIndex() * indicesPerTriangle;

	// Load indices
	const uint3 indices = LoadIndices(Indices, baseIndex);

	float3 interpolationFactors = CalculateBarycentricalInterpolationFactors(attribs.barycentrics);

	// Hit Normals
	float3 hitNormal = BarycentricInterpolation(Normals[indices[0]], Normals[indices[1]], Normals[indices[2]], interpolationFactors);

	// Shadow

	// Where is this thread's ray on screen?
	uint2 launchIndex = DispatchRaysIndex().xy;
	uint2 launchDim = DispatchRaysDimensions().xy;

	// Initialize a random seed, per-pixel, based on a screen position and temporally varying count
	uint randSeed = initRand(launchIndex.x + launchIndex.y * launchDim.x, FrameCount, 16);

	// Get random cone sample
	float3 randomLightDir = getConeSample(randSeed, hitPosition, LightPosition);

	// Trace ray
	Ray shadowRay = { hitPosition, randomLightDir };
	bool shadowRayHit = TraceShadowRayAndReportIfHit(shadowRay);

	float4 albedo = 0.9;

	// Hit Texcoords
	float2 hitUV = BarycentricInterpolation(Texcoords[indices[0]], Texcoords[indices[1]], Texcoords[indices[2]], interpolationFactors);
	albedo = DiffuseTexture.SampleLevel(DiffuseSampler, hitUV, 0);

	float4 color = CalculatePhongLighting(albedo, hitNormal, shadowRayHit, DiffuseCoef, SpecularCoef, SpecularPower);

	float roughness = RoughnessTexture.SampleLevel(DiffuseSampler, hitUV, 0).x;
	if (roughness > 0)
	{
		Ray reflectionRay;
		reflectionRay.origin = hitPosition;
		reflectionRay.direction = reflect(WorldRayDirection(), hitNormal);
		float4 reflectionColor = TraceRadianceRay(reflectionRay, payload.recursionDepth);

		float3 fresnelR = FresnelReflectanceSchlick(WorldRayDirection(), hitNormal, float3(0.5, 0.5, 0.5));
		float4 reflectedColor = ReflectanceCoef * float4(fresnelR, 1) * reflectionColor;
		color = lerp(color, reflectedColor, 0.5);
	}

	// AO
	float ambientOcclusion = 0.0f;
	for (int i = 0; i < NumRays; i++)
	{
		// Sample cosine-weighted hemisphere around surface normal to pick a random ray direction
		float3 aoDir = getCosHemisphereSample(randSeed, hitNormal);

		// Shoot our ambient occlusion ray and update the value we'll output with the result
		ambientOcclusion += TraceAORay(hitPosition, aoDir, AORayMin, AORadius);
	}

	float aoColor = ambientOcclusion / float(NumRays);
	color *= aoColor;

	// GI
	if (NumBounces > 0)
	{
		float3 giDir = getCosHemisphereSample(randSeed, hitNormal);
		color.xyz += albedo.xyz * TraceGIRay(hitPosition, giDir, randSeed, 0);
	}

	payload.color = color;
}

[shader("closesthit")]
void shadowChs(inout ShadowRayPayload payload, in BuiltInTriangleIntersectionAttributes attribs)
{
	payload.hit = true;
}

[shader("closesthit")]
void AOHit(inout AORayPayload payload, in BuiltInTriangleIntersectionAttributes attribs)
{
	payload.aoValue = 0.0f;
}

[shader("closesthit")]
void GIHit(inout GIRayPayload payload, in BuiltInTriangleIntersectionAttributes attribs)
{
	float3 hitPosition = WorldRayOrigin() + RayTCurrent() * WorldRayDirection();

	// Get the base index of the triangle's    
	uint indicesPerTriangle = 3;
	uint baseIndex = PrimitiveIndex() * indicesPerTriangle;

	// Load indices
	const uint3 indices = LoadIndices(Indices, baseIndex);

	float3 interpolationFactors = CalculateBarycentricalInterpolationFactors(attribs.barycentrics);

	// Hit Normals
	float3 hitNormal = BarycentricInterpolation(Normals[indices[0]], Normals[indices[1]], Normals[indices[2]], interpolationFactors);

	// Hit Texcoords
	float2 hitUV = BarycentricInterpolation(Texcoords[indices[0]], Texcoords[indices[1]], Texcoords[indices[2]], interpolationFactors);
	float4 albedo = DiffuseTexture.SampleLevel(DiffuseSampler, hitUV, 0);

	// Trace ray
	float3 randomLightDir = getConeSample(payload.seed, hitPosition, LightPosition);
	Ray shadowRay = { hitPosition, randomLightDir };
	bool shadowRayHit = TraceShadowRayAndReportIfHit(shadowRay);

	float4 color = CalculatePhongLighting(albedo, hitNormal, shadowRayHit, DiffuseCoef, SpecularCoef, SpecularPower);

	// AO
	float ambientOcclusion = 0.0f;
	for (int i = 0; i < NumRays; i++)
	{
		// Sample cosine-weighted hemisphere around surface normal to pick a random ray direction
		float3 aoDir = getCosHemisphereSample(payload.seed, hitNormal);

		// Shoot our ambient occlusion ray and update the value we'll output with the result
		ambientOcclusion += TraceAORay(hitPosition, aoDir, AORayMin, AORadius);
	}

	float aoColor = ambientOcclusion / float(NumRays);
	color *= aoColor;

	payload.color = color.xyz;

	/*if (payload.depth < NumBounces)
	{
		float3 giDir = getCosHemisphereSample(payload.seed, hitNormal);
		payload.color += albedo.xyz * TraceGIRay(hitPosition, giDir, payload.seed, payload.depth);
	}*/
}