using Shapes;
using Unity.VisualScripting;
using UnityEngine;

[ExecuteAlways]
public class ShapesDetails : ImmediateModeShapeDrawer
{
	public override void DrawShapes(Camera cam)
	{
		using (Draw.Command(cam))
		{
			Draw.LineGeometry = LineGeometry.Volumetric3D;
			Draw.ThicknessSpace = ThicknessSpace.Pixels;
			Draw.Thickness = 10f;
			Draw.BlendMode = ShapesBlendMode.Opaque;

			Draw.Matrix = transform.localToWorldMatrix;
			Draw.Line(Vector3.zero, new Vector3(10f, 10f, -10f), Color.cyan);
		}
	}
}
