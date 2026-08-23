using Godot;

namespace BreakerProtocol.Utils
{
	public static class AnchorPointMath
	{
		public const int GridUnitPixels = 80;

		/// <summary>
		/// 将局部像素点基于顺时针旋转 (0, 1, 2, 3) 变换到旋转后的包围盒内
		/// </summary>
		public static Vector2 TransformPixelPoint(Vector2 localPixel, int origGridW, int origGridH, int rotationSteps)
		{
			float wPx = origGridW * GridUnitPixels;
			float hPx = origGridH * GridUnitPixels;

			return (rotationSteps % 4) switch
			{
				1 => new Vector2(hPx - localPixel.Y, localPixel.X),        // 90°
				2 => new Vector2(wPx - localPixel.X, hPx - localPixel.Y),  // 180°
				3 => new Vector2(localPixel.Y, wPx - localPixel.X),        // 270°
				_ => localPixel                                           // 0°
			};
		}

		/// <summary>
		/// 像素坐标转换为网格局部整数索引
		/// </summary>
		public static Vector2I PixelToLocalGrid(Vector2 pixelPos)
		{
			return new Vector2I(
				Mathf.FloorToInt(pixelPos.X / GridUnitPixels),
				Mathf.FloorToInt(pixelPos.Y / GridUnitPixels)
			);
		}

		/// <summary>
		/// 将网格单元边缘吸附为最近的边缘枚举 (Top, Bottom, Left, Right)
		/// </summary>
		public static string GetClosestEdge(Vector2 localPixelInCell)
		{
			float x = Mathf.PosMod(localPixelInCell.X, GridUnitPixels);
			float y = Mathf.PosMod(localPixelInCell.Y, GridUnitPixels);

			float distTop = y;
			float distBottom = GridUnitPixels - y;
			float distLeft = x;
			float distRight = GridUnitPixels - x;

			float min = Mathf.Min(Mathf.Min(distTop, distBottom), Mathf.Min(distLeft, distRight));
			if (min == distTop) return "Top";
			if (min == distBottom) return "Bottom";
			if (min == distLeft) return "Left";
			return "Right";
		}
	}
}
