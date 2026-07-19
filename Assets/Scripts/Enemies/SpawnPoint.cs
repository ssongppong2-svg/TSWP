// 근거: 적 시스템.md — 적은 지정된 생성 지점 또는 화면 밖에서 등장한다.
// 씬에 배치해 SpawnManager가 수집한다.
using UnityEngine;

namespace TSWP.Enemies
{
    /// <summary>월드에 배치하는 적 생성 지점.</summary>
    public class SpawnPoint : MonoBehaviour
    {
        [Tooltip("화면 밖에서만 사용하는 지점인지 여부 — true면 카메라에 보일 때 이 지점을 쓰지 않는다.")]
        public bool offscreenOnly;

        private void OnDrawGizmos()
        {
            Gizmos.color = offscreenOnly ? Color.yellow : Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.4f);
        }
    }
}
