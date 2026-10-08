#ifndef LAVA_HEIGHT_FOG_INCLUDED
#define LAVA_HEIGHT_FOG_INCLUDED
            // Globals supplied by LavaHeightFog on the lava surface.
            float4 _LavaHeightFogParameters; // surface Y, height, density, contact softness
            half4 _LavaHeightFogColor;
            float LavaFogHash(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }
            // Procedural value noise substitutes for the reference's unavailable iChannel1.
            float LavaFogNoise(float3 p)
            {
                float3 cell = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float lo = lerp(lerp(LavaFogHash(cell), LavaFogHash(cell + float3(1,0,0)), f.x),
                    lerp(LavaFogHash(cell + float3(0,1,0)), LavaFogHash(cell + float3(1,1,0)), f.x), f.y);
                float hi = lerp(lerp(LavaFogHash(cell + float3(0,0,1)), LavaFogHash(cell + float3(1,0,1)), f.x),
                    lerp(LavaFogHash(cell + float3(0,1,1)), LavaFogHash(cell + float3(1,1,1)), f.x), f.y);
                return lerp(lo, hi, f.z);
            }
            float FogProfile(float y)
            {
                float above = max(0, y - _LavaHeightFogParameters.x);
                return exp(-3.0 * above / max(_LavaHeightFogParameters.y, 0.1));
            }
            half3 ApplyLavaHeightFog(half3 color, float3 positionWS)
            {
                if (_LavaHeightFogParameters.z <= 0 && _LavaHeightFogParameters.w <= 0) return color;
                float3 eye = GetCameraPositionWS();
                float lengthWS = min(distance(eye, positionWS), 80.0);
                float opticalDepth = 0;
                // Integrate the near-lava haze along the view ray, fading naturally above the liquid.
                [unroll] for (int i = 0; i < 8; i++)
                    opticalDepth += FogProfile(lerp(eye.y, positionWS.y, (i + 0.5) / 8.0));
                opticalDepth *= lengthWS / 8.0;
                float drift = LavaFogNoise(float3(positionWS.xz * 0.18, _Time.y * 0.035));
                float modulation = lerp(0.88, 1.12, drift);
                float haze = 1.0 - exp(-opticalDepth * _LavaHeightFogParameters.z * modulation);
                float contact = FogProfile(positionWS.y) * _LavaHeightFogParameters.w * modulation;
                float blend = saturate(1.0 - (1.0 - haze) * (1.0 - saturate(contact)));
                return lerp(color, _LavaHeightFogColor.rgb, blend);
            }
#endif
