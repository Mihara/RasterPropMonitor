
Shader "RPM/CroppedDisplayShader"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_Opacity("_Opacity", Range(0,1) ) = 1
		_Color ("_Color", Color) = (1,1,1,1)
		_CropBound ("_CropBound", Vector) = (-1,-1,1,1)
	}

	SubShader {

		Tags { "RenderType"="Overlay" "Queue" = "Transparent" }

		// Premultiplied Alpha shader for rendering/coloring textures.
		// Includes cropping values in normalized device coordinates

		Lighting Off
		Blend One OneMinusSrcAlpha
		Cull Back
		Fog { Mode Off }
		ZWrite Off
		ZTest Always

		Pass {
			Program "vp" {
// Vertex combos: 1
//   d3d9 - ALU: 9 to 9
//   d3d11 - ALU: 2 to 2, TEX: 0 to 0, FLOW: 1 to 1
SubProgram "opengl " {
Keywords { }
"!!GLSL
#ifdef VERTEX
varying vec2 xlv_TEXCOORD1;
varying vec2 xlv_TEXCOORD0;
uniform vec4 _MainTex_ST;

void main ()
{
  vec4 tmpvar_1;
  tmpvar_1 = (gl_ModelViewProjectionMatrix * gl_Vertex);
  gl_Position = tmpvar_1;
  xlv_TEXCOORD0 = ((gl_MultiTexCoord0.xy * _MainTex_ST.xy) + _MainTex_ST.zw);
  xlv_TEXCOORD1 = (tmpvar_1.xy / tmpvar_1.w);
}


#endif
#ifdef FRAGMENT
varying vec2 xlv_TEXCOORD1;
varying vec2 xlv_TEXCOORD0;
uniform float _Opacity;
uniform vec4 _CropBound;
uniform vec4 _Color;
uniform sampler2D _MainTex;
void main ()
{
  vec4 tmpvar_1;
  if (((((xlv_TEXCOORD1.x < _CropBound.x) || (xlv_TEXCOORD1.y < _CropBound.y)) || (xlv_TEXCOORD1.x > _CropBound.z)) || (xlv_TEXCOORD1.y > _CropBound.w))) {
    discard;
    tmpvar_1 = vec4(0.0, 0.0, 0.0, 0.0);
  } else {
    vec4 diffuse_2;
    vec4 tmpvar_3;
    tmpvar_3 = texture2D (_MainTex, xlv_TEXCOORD0);
    diffuse_2.xyz = tmpvar_3.xyz;
    diffuse_2.w = (tmpvar_3.w * (_Color.w * _Opacity));
    diffuse_2.xyz = ((tmpvar_3.xyz * _Color.xyz) * diffuse_2.w);
    tmpvar_1 = diffuse_2;
  };
  gl_FragData[0] = tmpvar_1;
}


#endif
"
}

SubProgram "d3d9 " {
Keywords { }
Bind "vertex" Vertex
Bind "texcoord" TexCoord0
Matrix 0 [glstate_matrix_mvp]
Vector 4 [_MainTex_ST]
"vs_3_0
; 9 ALU
dcl_position o0
dcl_texcoord0 o1
dcl_texcoord1 o2
dcl_position0 v0
dcl_texcoord0 v1
dp4 r0.x, v0, c3
rcp r1.x, r0.x
mov r0.w, r0.x
dp4 r0.x, v0, c0
dp4 r0.y, v0, c1
dp4 r0.z, v0, c2
mov o0, r0
mul o2.xy, r0, r1.x
mad o1.xy, v1, c4, c4.zwzw
"
}

SubProgram "d3d11 " {
Keywords { }
Bind "vertex" Vertex
Bind "texcoord" TexCoord0
ConstBuffer "$Globals" 80 // 32 used size, 5 vars
Vector 16 [_MainTex_ST] 4
ConstBuffer "UnityPerDraw" 336 // 64 used size, 6 vars
Matrix 0 [glstate_matrix_mvp] 4
BindCB "$Globals" 0
BindCB "UnityPerDraw" 1
// 8 instructions, 1 temp regs, 0 temp arrays:
// ALU 2 float, 0 int, 0 uint
// TEX 0 (0 load, 0 comp, 0 bias, 0 grad)
// FLOW 1 static, 0 dynamic
"vs_4_0
eefiecedeaojohcpahjifkmbiombkdikgmfejeofabaaaaaagaacaaaaadaaaaaa
cmaaaaaaiaaaaaaapaaaaaaaejfdeheoemaaaaaaacaaaaaaaiaaaaaadiaaaaaa
aaaaaaaaaaaaaaaaadaaaaaaaaaaaaaaapapaaaaebaaaaaaaaaaaaaaaaaaaaaa
adaaaaaaabaaaaaaadadaaaafaepfdejfeejepeoaafeeffiedepepfceeaaklkl
epfdeheogiaaaaaaadaaaaaaaiaaaaaafaaaaaaaaaaaaaaaabaaaaaaadaaaaaa
aaaaaaaaapaaaaaafmaaaaaaaaaaaaaaaaaaaaaaadaaaaaaabaaaaaaadamaaaa
fmaaaaaaabaaaaaaaaaaaaaaadaaaaaaabaaaaaaamadaaaafdfgfpfagphdgjhe
gjgpgoaafeeffiedepepfceeaaklklklfdeieefcgiabaaaaeaaaabaafkaaaaaa
fjaaaaaeegiocaaaaaaaaaaaacaaaaaafjaaaaaeegiocaaaabaaaaaaaeaaaaaa
fpaaaaadpcbabaaaaaaaaaaafpaaaaaddcbabaaaabaaaaaaghaaaaaepccabaaa
aaaaaaaaabaaaaaagfaaaaaddccabaaaabaaaaaagfaaaaadmccabaaaabaaaaaa
giaaaaacabaaaaaadiaaaaaipcaabaaaaaaaaaaafgbfbaaaaaaaaaaaegiocaaa
abaaaaaaabaaaaaadcaaaaakpcaabaaaaaaaaaaaegiocaaaabaaaaaaaaaaaaaa
agbabaaaaaaaaaaaegaobaaaaaaaaaaadcaaaaakpcaabaaaaaaaaaaaegiocaaa
abaaaaaaacaaaaaakgbkbaaaaaaaaaaaegaobaaaaaaaaaaadcaaaaakpcaabaaa
aaaaaaaaegiocaaaabaaaaaaadaaaaaapgbpbaaaaaaaaaaaegaobaaaaaaaaaaa
dgaaaaafpccabaaaaaaaaaaaegaobaaaaaaaaaaaaoaaaaahmccabaaaabaaaaaa
agaebaaaaaaaaaaapgapbaaaaaaaaaaadcaaaaaldccabaaaabaaaaaaegbabaaa
abaaaaaaegiacaaaaaaaaaaaabaaaaaaogikcaaaaaaaaaaaabaaaaaadoaaaaab
"
}

SubProgram "gles " {
Keywords { }
"!!GLES


#ifdef VERTEX

varying highp vec2 xlv_TEXCOORD1;
varying highp vec2 xlv_TEXCOORD0;
uniform highp vec4 _MainTex_ST;
uniform highp mat4 glstate_matrix_mvp;
attribute vec4 _glesMultiTexCoord0;
attribute vec4 _glesVertex;
void main ()
{
  highp vec4 tmpvar_1;
  tmpvar_1 = (glstate_matrix_mvp * _glesVertex);
  gl_Position = tmpvar_1;
  xlv_TEXCOORD0 = ((_glesMultiTexCoord0.xy * _MainTex_ST.xy) + _MainTex_ST.zw);
  xlv_TEXCOORD1 = (tmpvar_1.xy / tmpvar_1.w);
}



#endif
#ifdef FRAGMENT

varying highp vec2 xlv_TEXCOORD1;
varying highp vec2 xlv_TEXCOORD0;
uniform highp float _Opacity;
uniform highp vec4 _CropBound;
uniform highp vec4 _Color;
uniform sampler2D _MainTex;
void main ()
{
  highp vec4 tmpvar_1;
  if (((((xlv_TEXCOORD1.x < _CropBound.x) || (xlv_TEXCOORD1.y < _CropBound.y)) || (xlv_TEXCOORD1.x > _CropBound.z)) || (xlv_TEXCOORD1.y > _CropBound.w))) {
    discard;
    tmpvar_1 = vec4(0.0, 0.0, 0.0, 0.0);
  } else {
    highp vec4 diffuse_2;
    lowp vec4 tmpvar_3;
    tmpvar_3 = texture2D (_MainTex, xlv_TEXCOORD0);
    diffuse_2 = tmpvar_3;
    diffuse_2.w = (diffuse_2.w * (_Color.w * _Opacity));
    diffuse_2.xyz = ((diffuse_2.xyz * _Color.xyz) * diffuse_2.w);
    tmpvar_1 = diffuse_2;
  };
  gl_FragData[0] = tmpvar_1;
}



#endif"
}

SubProgram "glesdesktop " {
Keywords { }
"!!GLES


#ifdef VERTEX

varying highp vec2 xlv_TEXCOORD1;
varying highp vec2 xlv_TEXCOORD0;
uniform highp vec4 _MainTex_ST;
uniform highp mat4 glstate_matrix_mvp;
attribute vec4 _glesMultiTexCoord0;
attribute vec4 _glesVertex;
void main ()
{
  highp vec4 tmpvar_1;
  tmpvar_1 = (glstate_matrix_mvp * _glesVertex);
  gl_Position = tmpvar_1;
  xlv_TEXCOORD0 = ((_glesMultiTexCoord0.xy * _MainTex_ST.xy) + _MainTex_ST.zw);
  xlv_TEXCOORD1 = (tmpvar_1.xy / tmpvar_1.w);
}



#endif
#ifdef FRAGMENT

varying highp vec2 xlv_TEXCOORD1;
varying highp vec2 xlv_TEXCOORD0;
uniform highp float _Opacity;
uniform highp vec4 _CropBound;
uniform highp vec4 _Color;
uniform sampler2D _MainTex;
void main ()
{
  highp vec4 tmpvar_1;
  if (((((xlv_TEXCOORD1.x < _CropBound.x) || (xlv_TEXCOORD1.y < _CropBound.y)) || (xlv_TEXCOORD1.x > _CropBound.z)) || (xlv_TEXCOORD1.y > _CropBound.w))) {
    discard;
    tmpvar_1 = vec4(0.0, 0.0, 0.0, 0.0);
  } else {
    highp vec4 diffuse_2;
    lowp vec4 tmpvar_3;
    tmpvar_3 = texture2D (_MainTex, xlv_TEXCOORD0);
    diffuse_2 = tmpvar_3;
    diffuse_2.w = (diffuse_2.w * (_Color.w * _Opacity));
    diffuse_2.xyz = ((diffuse_2.xyz * _Color.xyz) * diffuse_2.w);
    tmpvar_1 = diffuse_2;
  };
  gl_FragData[0] = tmpvar_1;
}



#endif"
}

SubProgram "gles3 " {
Keywords { }
"!!GLES3#version 300 es


#ifdef VERTEX

#define gl_Vertex _glesVertex
in vec4 _glesVertex;
#define gl_MultiTexCoord0 _glesMultiTexCoord0
in vec4 _glesMultiTexCoord0;

#line 150
struct v2f_vertex_lit {
    highp vec2 uv;
    lowp vec4 diff;
    lowp vec4 spec;
};
#line 186
struct v2f_img {
    highp vec4 pos;
    mediump vec2 uv;
};
#line 180
struct appdata_img {
    highp vec4 vertex;
    mediump vec2 texcoord;
};
#line 312
struct v2f {
    highp vec4 vertex;
    highp vec2 texcoord;
    highp vec2 pixelPos;
};
#line 306
struct appdata_t {
    highp vec4 vertex;
    highp vec2 texcoord;
};
uniform highp vec4 _Time;
uniform highp vec4 _SinTime;
#line 3
uniform highp vec4 _CosTime;
uniform highp vec4 unity_DeltaTime;
uniform highp vec3 _WorldSpaceCameraPos;
uniform highp vec4 _ProjectionParams;
#line 7
uniform highp vec4 _ScreenParams;
uniform highp vec4 _ZBufferParams;
uniform highp vec4 unity_CameraWorldClipPlanes[6];
uniform highp vec4 _WorldSpaceLightPos0;
#line 11
uniform highp vec4 _LightPositionRange;
uniform highp vec4 unity_4LightPosX0;
uniform highp vec4 unity_4LightPosY0;
uniform highp vec4 unity_4LightPosZ0;
#line 15
uniform highp vec4 unity_4LightAtten0;
uniform highp vec4 unity_LightColor[4];
uniform highp vec4 unity_LightPosition[4];
uniform highp vec4 unity_LightAtten[4];
#line 19
uniform highp vec4 unity_SHAr;
uniform highp vec4 unity_SHAg;
uniform highp vec4 unity_SHAb;
uniform highp vec4 unity_SHBr;
#line 23
uniform highp vec4 unity_SHBg;
uniform highp vec4 unity_SHBb;
uniform highp vec4 unity_SHC;
uniform highp vec3 unity_LightColor0;
uniform highp vec3 unity_LightColor1;
uniform highp vec3 unity_LightColor2;
uniform highp vec3 unity_LightColor3;
#line 27
uniform highp vec4 unity_ShadowSplitSpheres[4];
uniform highp vec4 unity_ShadowSplitSqRadii;
uniform highp vec4 unity_LightShadowBias;
uniform highp vec4 _LightSplitsNear;
#line 31
uniform highp vec4 _LightSplitsFar;
uniform highp mat4 unity_World2Shadow[4];
uniform highp vec4 _LightShadowData;
uniform highp vec4 unity_ShadowFadeCenterAndType;
#line 35
uniform highp mat4 glstate_matrix_mvp;
uniform highp mat4 glstate_matrix_modelview0;
uniform highp mat4 glstate_matrix_invtrans_modelview0;
uniform highp mat4 _Object2World;
#line 39
uniform highp mat4 _World2Object;
uniform highp vec4 unity_Scale;
uniform highp mat4 glstate_matrix_transpose_modelview0;
uniform highp mat4 glstate_matrix_texture0;
#line 43
uniform highp mat4 glstate_matrix_texture1;
uniform highp mat4 glstate_matrix_texture2;
uniform highp mat4 glstate_matrix_texture3;
uniform highp mat4 glstate_matrix_projection;
#line 47
uniform highp vec4 glstate_lightmodel_ambient;
uniform highp mat4 unity_MatrixV;
uniform highp mat4 unity_MatrixVP;
uniform lowp vec4 unity_ColorSpaceGrey;
#line 76
#line 81
#line 86
#line 90
#line 95
#line 119
#line 136
#line 157
#line 165
#line 192
#line 205
#line 214
#line 219
#line 228
#line 233
#line 242
#line 259
#line 264
#line 290
#line 298
#line 302
#line 319
uniform sampler2D _MainTex;
uniform highp vec4 _MainTex_ST;
uniform highp vec4 _Color;
uniform highp vec4 _CropBound;
#line 323
uniform highp float _Opacity;
#line 332
#line 324
v2f vert( in appdata_t v ) {
    v2f o;
    #line 327
    o.vertex = (glstate_matrix_mvp * v.vertex);
    o.texcoord = ((v.texcoord.xy * _MainTex_ST.xy) + _MainTex_ST.zw);
    o.pixelPos = (o.vertex.xy / o.vertex.w);
    return o;
}
out highp vec2 xlv_TEXCOORD0;
out highp vec2 xlv_TEXCOORD1;
void main() {
    v2f xl_retval;
    appdata_t xlt_v;
    xlt_v.vertex = vec4(gl_Vertex);
    xlt_v.texcoord = vec2(gl_MultiTexCoord0);
    xl_retval = vert( xlt_v);
    gl_Position = vec4(xl_retval.vertex);
    xlv_TEXCOORD0 = vec2(xl_retval.texcoord);
    xlv_TEXCOORD1 = vec2(xl_retval.pixelPos);
}


#endif
#ifdef FRAGMENT

#define gl_FragData _glesFragData
layout(location = 0) out mediump vec4 _glesFragData[4];

#line 150
struct v2f_vertex_lit {
    highp vec2 uv;
    lowp vec4 diff;
    lowp vec4 spec;
};
#line 186
struct v2f_img {
    highp vec4 pos;
    mediump vec2 uv;
};
#line 180
struct appdata_img {
    highp vec4 vertex;
    mediump vec2 texcoord;
};
#line 312
struct v2f {
    highp vec4 vertex;
    highp vec2 texcoord;
    highp vec2 pixelPos;
};
#line 306
struct appdata_t {
    highp vec4 vertex;
    highp vec2 texcoord;
};
uniform highp vec4 _Time;
uniform highp vec4 _SinTime;
#line 3
uniform highp vec4 _CosTime;
uniform highp vec4 unity_DeltaTime;
uniform highp vec3 _WorldSpaceCameraPos;
uniform highp vec4 _ProjectionParams;
#line 7
uniform highp vec4 _ScreenParams;
uniform highp vec4 _ZBufferParams;
uniform highp vec4 unity_CameraWorldClipPlanes[6];
uniform highp vec4 _WorldSpaceLightPos0;
#line 11
uniform highp vec4 _LightPositionRange;
uniform highp vec4 unity_4LightPosX0;
uniform highp vec4 unity_4LightPosY0;
uniform highp vec4 unity_4LightPosZ0;
#line 15
uniform highp vec4 unity_4LightAtten0;
uniform highp vec4 unity_LightColor[4];
uniform highp vec4 unity_LightPosition[4];
uniform highp vec4 unity_LightAtten[4];
#line 19
uniform highp vec4 unity_SHAr;
uniform highp vec4 unity_SHAg;
uniform highp vec4 unity_SHAb;
uniform highp vec4 unity_SHBr;
#line 23
uniform highp vec4 unity_SHBg;
uniform highp vec4 unity_SHBb;
uniform highp vec4 unity_SHC;
uniform highp vec3 unity_LightColor0;
uniform highp vec3 unity_LightColor1;
uniform highp vec3 unity_LightColor2;
uniform highp vec3 unity_LightColor3;
#line 27
uniform highp vec4 unity_ShadowSplitSpheres[4];
uniform highp vec4 unity_ShadowSplitSqRadii;
uniform highp vec4 unity_LightShadowBias;
uniform highp vec4 _LightSplitsNear;
#line 31
uniform highp vec4 _LightSplitsFar;
uniform highp mat4 unity_World2Shadow[4];
uniform highp vec4 _LightShadowData;
uniform highp vec4 unity_ShadowFadeCenterAndType;
#line 35
uniform highp mat4 glstate_matrix_mvp;
uniform highp mat4 glstate_matrix_modelview0;
uniform highp mat4 glstate_matrix_invtrans_modelview0;
uniform highp mat4 _Object2World;
#line 39
uniform highp mat4 _World2Object;
uniform highp vec4 unity_Scale;
uniform highp mat4 glstate_matrix_transpose_modelview0;
uniform highp mat4 glstate_matrix_texture0;
#line 43
uniform highp mat4 glstate_matrix_texture1;
uniform highp mat4 glstate_matrix_texture2;
uniform highp mat4 glstate_matrix_texture3;
uniform highp mat4 glstate_matrix_projection;
#line 47
uniform highp vec4 glstate_lightmodel_ambient;
uniform highp mat4 unity_MatrixV;
uniform highp mat4 unity_MatrixVP;
uniform lowp vec4 unity_ColorSpaceGrey;
#line 76
#line 81
#line 86
#line 90
#line 95
#line 119
#line 136
#line 157
#line 165
#line 192
#line 205
#line 214
#line 219
#line 228
#line 233
#line 242
#line 259
#line 264
#line 290
#line 298
#line 302
#line 319
uniform sampler2D _MainTex;
uniform highp vec4 _MainTex_ST;
uniform highp vec4 _Color;
uniform highp vec4 _CropBound;
#line 323
uniform highp float _Opacity;
#line 332
#line 332
highp vec4 frag( in v2f i ) {
    if (((((i.pixelPos.x < _CropBound.x) || (i.pixelPos.y < _CropBound.y)) || (i.pixelPos.x > _CropBound.z)) || (i.pixelPos.y > _CropBound.w))){
        #line 336
        discard;
        return vec4( 0.0);
    }
    else{
        #line 341
        highp vec4 diffuse = texture( _MainTex, i.texcoord);
        diffuse.w *= (_Color.w * _Opacity);
        diffuse.xyz = ((diffuse.xyz * _Color.xyz) * diffuse.w);
        return diffuse;
    }
}
in highp vec2 xlv_TEXCOORD0;
in highp vec2 xlv_TEXCOORD1;
void main() {
    highp vec4 xl_retval;
    v2f xlt_i;
    xlt_i.vertex = vec4(0.0);
    xlt_i.texcoord = vec2(xlv_TEXCOORD0);
    xlt_i.pixelPos = vec2(xlv_TEXCOORD1);
    xl_retval = frag( xlt_i);
    gl_FragData[0] = vec4(xl_retval);
}


#endif"
}

}
Program "fp" {
// Fragment combos: 1
//   d3d9 - ALU: 19 to 19, TEX: 2 to 2, FLOW: 2 to 2
//   d3d11 - ALU: 9 to 9, TEX: 1 to 1, FLOW: 4 to 4
SubProgram "opengl " {
Keywords { }
"!!GLSL"
}

SubProgram "d3d9 " {
Keywords { }
Vector 0 [_Color]
Vector 1 [_CropBound]
Float 2 [_Opacity]
SetTexture 0 [_MainTex] 2D
"ps_3_0
; 19 ALU, 2 TEX, 2 FLOW
dcl_2d s0
def c3, 0.00000000, 1.00000000, 0, 0
dcl_texcoord0 v0.xy
dcl_texcoord1 v1.xy
add r0.y, v1, -c1
add r0.x, v1, -c1
add r0.z, -v1.y, c1.w
cmp r0.y, r0, c3.x, c3
cmp r0.x, r0, c3, c3.y
add_pp_sat r0.x, r0, r0.y
add r0.y, -v1.x, c1.z
cmp r0.y, r0, c3.x, c3
cmp r0.z, r0, c3.x, c3.y
add_pp_sat r0.x, r0, r0.y
add_pp_sat r0.x, r0, r0.z
if_gt r0.x, c3.x
mov_pp r0, -c3.y
texkill r0.xyzw
mov oC0, c3.x
else
mov r0.x, c0.w
mul r1.x, c2, r0
texld r0, v0, s0
mul r0.w, r0, r1.x
mul r0.xyz, r0, c0
mul oC0.xyz, r0, r0.w
mov oC0.w, r0
endif
"
}

SubProgram "d3d11 " {
Keywords { }
ConstBuffer "$Globals" 80 // 68 used size, 5 vars
Vector 32 [_Color] 4
Vector 48 [_CropBound] 4
Float 64 [_Opacity]
BindCB "$Globals" 0
SetTexture 0 [_MainTex] 2D 0
// 19 instructions, 2 temp regs, 0 temp arrays:
// ALU 6 float, 0 int, 3 uint
// TEX 1 (0 load, 0 comp, 0 bias, 0 grad)
// FLOW 3 static, 1 dynamic
"ps_4_0
eefiecedhjmgadnddamajcobhegfejdceeajafceabaaaaaammacaaaaadaaaaaa
cmaaaaaajmaaaaaanaaaaaaaejfdeheogiaaaaaaadaaaaaaaiaaaaaafaaaaaaa
aaaaaaaaabaaaaaaadaaaaaaaaaaaaaaapaaaaaafmaaaaaaaaaaaaaaaaaaaaaa
adaaaaaaabaaaaaaadadaaaafmaaaaaaabaaaaaaaaaaaaaaadaaaaaaabaaaaaa
amamaaaafdfgfpfagphdgjhegjgpgoaafeeffiedepepfceeaaklklklepfdeheo
cmaaaaaaabaaaaaaaiaaaaaacaaaaaaaaaaaaaaaaaaaaaaaadaaaaaaaaaaaaaa
apaaaaaafdfgfpfegbhcghgfheaaklklfdeieefcpeabaaaaeaaaaaaahnaaaaaa
fjaaaaaeegiocaaaaaaaaaaaafaaaaaafkaaaaadaagabaaaaaaaaaaafibiaaae
aahabaaaaaaaaaaaffffaaaagcbaaaaddcbabaaaabaaaaaagcbaaaadmcbabaaa
abaaaaaagfaaaaadpccabaaaaaaaaaaagiaaaaacacaaaaaadbaaaaaidcaabaaa
aaaaaaaaogbkbaaaabaaaaaaegiacaaaaaaaaaaaadaaaaaadmaaaaahbcaabaaa
aaaaaaaabkaabaaaaaaaaaaaakaabaaaaaaaaaaadbaaaaaigcaabaaaaaaaaaaa
kgilcaaaaaaaaaaaadaaaaaakgblbaaaabaaaaaadmaaaaahbcaabaaaaaaaaaaa
bkaabaaaaaaaaaaaakaabaaaaaaaaaaadmaaaaahbcaabaaaaaaaaaaackaabaaa
aaaaaaaaakaabaaaaaaaaaaabpaaaeadakaabaaaaaaaaaaaanaaaeadabeaaaaa
ppppppppdgaaaaaipccabaaaaaaaaaaaaceaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
aaaaaaaadoaaaaabbcaaaaabefaaaaajpcaabaaaaaaaaaaaegbabaaaabaaaaaa
eghobaaaaaaaaaaaaagabaaaaaaaaaaadiaaaaajbcaabaaaabaaaaaadkiacaaa
aaaaaaaaacaaaaaaakiacaaaaaaaaaaaaeaaaaaadiaaaaahicaabaaaaaaaaaaa
dkaabaaaaaaaaaaaakaabaaaabaaaaaadiaaaaaihcaabaaaaaaaaaaaegacbaaa
aaaaaaaaegiccaaaaaaaaaaaacaaaaaadiaaaaahhccabaaaaaaaaaaapgapbaaa
aaaaaaaaegacbaaaaaaaaaaadgaaaaaficcabaaaaaaaaaaadkaabaaaaaaaaaaa
doaaaaabbfaaaaabdoaaaaab"
}

SubProgram "gles " {
Keywords { }
"!!GLES"
}

SubProgram "glesdesktop " {
Keywords { }
"!!GLES"
}

SubProgram "gles3 " {
Keywords { }
"!!GLES3"
}

}

#LINE 80

		}
	}

	Fallback off
}
