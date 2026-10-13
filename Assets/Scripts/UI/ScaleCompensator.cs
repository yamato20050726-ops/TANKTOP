using UnityEngine;

/// <summary>
/// 親オブジェクトのTransform Scaleを変えても、このオブジェクト自身の見た目の大きさ
/// （ワールド上での実際のサイズ）が変わらないようにする。
///
/// 例：SkillBoxの中央に表示する数字（HitCountCanvas）に付けておくと、
/// SkillBox自体のScaleを大きく/小さくしても、数字の大きさはTarget Local Scaleで
/// 指定した通りに保たれる（親のScaleと数字の見た目サイズが連動しなくなる）。
///
/// 使い方：
/// 親のScaleに影響されたくないオブジェクトにこのスクリプトを付け、
/// Target Local Scaleに「親のScaleが1のときに、このオブジェクトが本来持つべき
/// Local Scale」を設定する。
/// </summary>
public class ScaleCompensator : MonoBehaviour
{
    [Tooltip("親のScaleが(1,1,1)のときの、このオブジェクト本来のLocal Scale")]
    public Vector3 targetLocalScale = new Vector3(0.01f, 0.01f, 0.01f);

    void LateUpdate()
    {
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;

        transform.localScale = new Vector3(
            parentScale.x != 0f ? targetLocalScale.x / parentScale.x : targetLocalScale.x,
            parentScale.y != 0f ? targetLocalScale.y / parentScale.y : targetLocalScale.y,
            parentScale.z != 0f ? targetLocalScale.z / parentScale.z : targetLocalScale.z
        );
    }
}
