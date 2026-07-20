// 근거: 적 시스템.md — 적은 지정된 생성 지점 또는 화면 밖에서 등장한다.
// 씬(또는 방 프리팹)에 배치하면 스스로 SpawnManager에 등록된다 —
// 방을 활성화하는 쪽(Map)이 SpawnManager를 알 필요가 없고, 매니저보다 먼저 켜져도 안전하다.
using UnityEngine;

namespace TSWP.Enemies
{
    /// <summary>월드에 배치하는 적 생성 지점.</summary>
    public class SpawnPoint : MonoBehaviour
    {
        [Tooltip("화면 밖에서만 사용하는 지점인지 여부 — true면 카메라에 보일 때 이 지점을 쓰지 않는다.")]
        public bool offscreenOnly;

        private void OnEnable() => SpawnManager.RegisterSpawnPoint(this);

        // 방이 비활성화되면 그 방의 지점도 후보에서 빠져야 한다 — 다른 방에 적이 튀어나오지 않게 한다.
        private void OnDisable() => SpawnManager.UnregisterSpawnPoint(this);

        private void OnDrawGizmos()
        {
            Gizmos.color = offscreenOnly ? Color.yellow : Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.4f);
        }
    }
}
