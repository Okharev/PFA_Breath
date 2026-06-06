Shader "Hidden/VertexColorPreview"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+1" }
        // Pulls the preview slightly towards the camera to stop Z-fighting
        Offset -1, -1 

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR; // Extracts the array we modify in C#
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return i.color; 
            }
            ENDCG
        }
    }
}