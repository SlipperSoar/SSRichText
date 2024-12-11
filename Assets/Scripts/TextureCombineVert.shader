Shader "Custom/TextureCombineVert"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _StencilComp ("Stencil Comparison", Float) = 8
	    _Stencil ("Stencil ID", Float) = 0
	    _StencilOp ("Stencil Operation", Float) = 0
	    _StencilWriteMask ("Stencil Write Mask", Float) = 255
	    _StencilReadMask ("Stencil Read Mask", Float) = 255
	    _ColorMask ("Color Mask", Float) = 15
        
        _Offset ("Offset", Vector) = (0,0,0,0)
        _Scale ("Scale", Vector) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" }

        Pass
        {
            ZWrite Off                // 关闭深度写入
            Lighting Off
            Cull Off                  // 不剔除任何面
            ZTest [unity_GUIZTestMode] // 用于UI组件的shader都要包含一句：ZTest [unity_GUIZTestMode]，以确保UI能在前层显示
            Blend SrcAlpha OneMinusSrcAlpha // 混合模式：基于源的Alpha值混合
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Offset;
            float4 _Scale;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);// 实例化处理
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

                v.vertex.x = v.vertex.x * _Scale.x + _Offset.x;
                v.vertex.y = v.vertex.y * _Scale.y + _Offset.y;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                return tex2D(_MainTex, i.texcoord);
            }
            ENDCG
        }
    }
}
