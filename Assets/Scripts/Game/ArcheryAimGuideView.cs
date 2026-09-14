using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 당기는 동안 화살이 그릴 길을 미리 선으로 보여 준다.
    ///
    /// <para>화살이 <b>포물선</b>으로 날아가서 화면 한가운데가 곧 맞는 자리가 아니다 — 십자선을
    /// 띄워 봐야 거짓말이 된다. 그래서 시뮬이 쓰는 바로 그 함수(<see cref="ArcheryTrajectory"/>)에
    /// 지금 조준·당김을 넣어 길을 뽑는다. <b>보이는 선이 곧 화살이 가는 길</b>이다.</para>
    ///
    /// <para>당긴 정도에 따라 속도가 달라지므로 선도 같이 길어진다 — 얼마나 당겼는지가 화면에
    /// 그대로 보인다.</para>
    /// </summary>
    public class ArcheryAimGuideView : ILateTickable, System.IDisposable
    {
        //  길수록 더 멀리까지 보여 주지만, 끝까지 그리면 어디를 노리는지가 오히려 안 읽힌다.
        private const float PreviewSeconds = 1.2f;
        private const int SampleCount = 24;
        private const float LineWidth = 0.05f;

        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        private LineRenderer _line;
        private Material _lineMaterial;

        public ArcheryAimGuideView(IPlayerContext playerContext,
                                   GameFramework.World.EntityRegistry entityRegistry)
        {
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
        }

        public void LateTick()
        {
            var aim = MyAim();
            //  임계치를 못 넘었으면 아직 시위가 안 걸린 것이다 — 쏘지도 않을 길을 그리지 않는다.
            if (aim == null || aim.Drawing == false || aim.DrawRatio < ArcheryAimSystem.DrawThreshold)
            {
                Hide();
                return;
            }

            var entity = entityRegistry.Get(playerContext.entityId);
            if (entity == null || entity.Has<GameFramework.World.Transform>() == false)
            {
                Hide();
                return;
            }

            //  속도는 시뮬이 쏠 때 쓰는 것과 같은 값에서 나와야 한다 — 선이 거짓말을 하면
            //  조준선이 있으나 마나다. 당김은 손가락이 끈 거리가 정하므로 그 값을 그대로 읽는다.
            float speed = ArcheryAimSystem.SpeedFor(aim.DrawRatio);

            //  발사할 때와 같은 원점·속도로 만든다. 여기가 어긋나면 선이 거짓말을 한다.
            var shot = new ArcheryShot(
                playerContext.entityId,
                0,
                GameFramework.World.EntityMotionExtensions.GetPosition(entity)
                    + new Vector3(0f, ArcheryAimSystem.EyeHeight, 0f),
                ArcheryTrajectory.DirectionFrom(aim.Yaw, aim.Pitch) * speed);

            Draw(shot);
        }

        private void Draw(in ArcheryShot shot)
        {
            var line = Line();
            line.enabled = true;
            line.positionCount = SampleCount;
            for (int i = 0; i < SampleCount; i++)
            {
                float seconds = PreviewSeconds * i / (SampleCount - 1);
                line.SetPosition(i, ArcheryTrajectory.PositionAt(shot, seconds));
            }
        }

        private ArcheryAim MyAim()
        {
            if (playerContext.entityId == null)
            {
                return null;
            }
            return entityRegistry.Get(playerContext.entityId)?.Get<ArcheryAim>();
        }

        private void Hide()
        {
            if (_line != null)
            {
                _line.enabled = false;
            }
        }

        private LineRenderer Line()
        {
            if (_line == null)
            {
                var go = new GameObject("ArcheryAimGuide");
                _line = go.AddComponent<LineRenderer>();
                _line.useWorldSpace = true;
                _line.widthMultiplier = LineWidth;
                _line.numCapVertices = 2;
                _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _line.receiveShadows = false;
                _line.sharedMaterial = LineMaterial();
            }
            return _line;
        }

        private Material LineMaterial()
        {
            if (_lineMaterial == null)
            {
                //  선은 빛을 받을 이유가 없다 — Unlit이라야 어두운 곳에서도 같은 색으로 보인다.
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                //  과녁(노랑)·함정(파랑)·화살(빨강) 어느 것과도 안 겹치게 흰색에 가깝게 둔다.
                _lineMaterial = new Material(shader) { color = new Color(1f, 1f, 1f, 0.7f) };
            }
            return _lineMaterial;
        }

        public void Dispose()
        {
            if (_line != null)
            {
                Object.Destroy(_line.gameObject);
                _line = null;
            }
            if (_lineMaterial != null)
            {
                Object.Destroy(_lineMaterial);
                _lineMaterial = null;
            }
        }
    }
}
