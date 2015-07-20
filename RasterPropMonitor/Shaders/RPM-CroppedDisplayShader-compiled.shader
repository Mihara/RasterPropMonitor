
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
//   opengl - ALU: 9 to 9
//   d3d9 - ALU: 9 to 9
//   d3d11 - ALU: 2 to 2, TEX: 0 to 0, FLOW: 1 to 1
SubProgram "opengl " {
Keywords { }
Bind "vertex" Vertex
Bind "texcoord" TexCoord0
Vector 5 [_MainTex_ST]
"3.0-!!ARBvp1.0
# 9 ALU
PARAM c[6] = { program.local[0],
		state.matrix.mvp,
		program.local[5] };
TEMP R0;
TEMP R1;
DP4 R0.x, vertex.position, c[4];
RCP R1.x, R0.x;
MOV R0.w, R0.x;
DP4 R0.x, vertex.position, c[1];
DP4 R0.y, vertex.position, c[2];
DP4 R0.z, vertex.position, c[3];
MOV result.position, R0;
MUL result.texcoord[1].xy, R0, R1.x;
MAD result.texcoord[0].xy, vertex.texcoord[0], c[5], c[5].zwzw;
END
# 9 instructions, 2 R-regs
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

}
Program "fp" {
// Fragment combos: 1
//   opengl - ALU: 17 to 17, TEX: 1 to 1
//   d3d9 - ALU: 19 to 19, TEX: 2 to 2, FLOW: 2 to 2
//   d3d11 - ALU: 9 to 9, TEX: 1 to 1, FLOW: 4 to 4
SubProgram "opengl " {
Keywords { }
Vector 0 [_Color]
Vector 1 [_CropBound]
Float 2 [_Opacity]
SetTexture 0 [_MainTex] 2D
"3.0-!!ARBfp1.0
# 17 ALU, 1 TEX
PARAM c[4] = { program.local[0..2],
		{ 1, 0 } };
TEMP R0;
TEMP R1;
TEX R0, fragment.texcoord[0], texture[0], 2D;
MOV R1.x, c[2];
MUL R1.x, R1, c[0].w;
MUL R1.x, R0.w, R1;
MUL R0.xyz, R0, c[0];
SLT R1.y, fragment.texcoord[1], c[1];
SLT R0.w, fragment.texcoord[1].x, c[1].x;
ADD_SAT R0.w, R0, R1.y;
SLT R1.y, c[1].z, fragment.texcoord[1].x;
ADD_SAT R0.w, R0, R1.y;
SLT R1.z, c[1].w, fragment.texcoord[1].y;
ADD_SAT R0.w, R0, R1.z;
MUL R1.yzw, R0.xxyz, R1.x;
ABS R0.x, R0.w;
CMP R0.x, -R0, c[3].y, c[3];
CMP result.color, -R0.x, R1.yzwx, c[3].y;
KIL -R0.w;
END
# 17 instructions, 2 R-regs
"
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

}

#LINE 80
 
		}
	} 	
	
	Fallback off 
}
