namespace UnityTools.Math
{
	public class Tool
	{
		public static float3 ProjectionOnPlane(float3 _v, float3 _normal)
		{
			//return math.normalize(_v - (math.dot(_v, _normal) * _normal));
			return _v - (math.dot(_v, _normal) * _normal);
		}
		
		public static float CosAngle(float3 _from, float3 _to)
		{
			//float num = (float)math.sqrt((double)(math.lengthsq(_from) * math.lengthsq(_to)));
			//if (num < 1E-15f)
			//{
			//	return 0f;
			//}
			//return Mathf.Clamp(math.dot(_from, _to) / num, -1f, 1f);
			return math.dot(_from, _to);
		}

		public static float Angle(float3 _from, float3 _to)
		{
			return (float)(math.acos((double)CosAngle(_from, _to)) * (180 / math.PI_DBL));
		}

		public static float3 ProjectionOnVector(float3 _v1, float3 _v2)
		{
			return math.dot(_v2, _v1) / math.length(_v2);
		}

		public static float3 Normal(float3 _v1, float3 _v2)
		{
			return math.normalize(math.cross(_v1, _v2));
		}
	}
}
