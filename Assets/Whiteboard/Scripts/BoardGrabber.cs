using UnityEngine;

namespace QuestWhiteboard
{
    /// <summary>
    /// グリップ(中指のボタン)でボードをつかんで動かす。
    ///   片手でグリップ … ボードがその手についてくる(位置と向き)
    ///   両手でグリップ … 手の間隔を広げると拡大、狭めると縮小
    /// 起動時はボードを目の前 1.8m に置く。
    /// </summary>
    public class BoardGrabber : MonoBehaviour
    {
        public Transform leftHand;
        public Transform rightHand;
        public Transform head;
        public float startDistance = 1.8f;
        public float minScale = 0.25f, maxScale = 6f;

        Transform grabbingHand;
        Vector3 localPosInHand;
        Quaternion localRotInHand;

        bool twoHand;
        float startHandDist, startScale;
        Vector3 startMidLocal; // 両手の中点をボードのローカル座標で覚えておく

        System.Collections.IEnumerator Start()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            yield return null; // ヘッドセットの位置が確定するのを1フレーム待つ
            yield return null;
            PlaceInFront();
        }

        public void PlaceInFront()
        {
            if (head == null) return;
            var fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            transform.position = head.position + fwd * startDistance;
            // ボードの表(-forward)がこちらを向くように
            transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        void Update()
        {
            bool l = leftHand != null && OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.LTouch) > 0.6f;
            bool r = rightHand != null && OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.RTouch) > 0.6f;

            if (l && r)
            {
                if (!twoHand) BeginTwoHand();
                UpdateTwoHand();
                return;
            }
            if (twoHand)
            {
                twoHand = false;
                grabbingHand = null; // 片手に戻ったら掴み直す
            }

            Transform hand = r ? rightHand : (l ? leftHand : null);
            if (hand == null) { grabbingHand = null; return; }
            if (grabbingHand != hand)
            {
                grabbingHand = hand;
                localPosInHand = Quaternion.Inverse(hand.rotation) * (transform.position - hand.position);
                localRotInHand = Quaternion.Inverse(hand.rotation) * transform.rotation;
            }
            transform.SetPositionAndRotation(hand.position + hand.rotation * localPosInHand, hand.rotation * localRotInHand);
        }

        void BeginTwoHand()
        {
            twoHand = true;
            startHandDist = Mathf.Max(0.05f, Vector3.Distance(leftHand.position, rightHand.position));
            startScale = transform.localScale.x;
            startMidLocal = transform.InverseTransformPoint((leftHand.position + rightHand.position) * 0.5f);
        }

        void UpdateTwoHand()
        {
            float d = Vector3.Distance(leftHand.position, rightHand.position);
            float s = Mathf.Clamp(startScale * d / startHandDist, minScale, maxScale);
            transform.localScale = Vector3.one * s;
            // 両手の中点の下にあったボード上の点が、そのまま中点についてくるようにする
            Vector3 mid = (leftHand.position + rightHand.position) * 0.5f;
            transform.position += mid - transform.TransformPoint(startMidLocal);
        }
    }
}
