using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace LOP
{
    /// <summary>
    /// 로비가 보는 "내 캐릭터 외형"(피부·머리 색 번호). 조회와 라커룸 변경 응답을 한곳에 모은다.
    /// 조회가 실패하면 이전 값을 그대로 둔다. 받기 전엔 0(모름) — 그리는 쪽이 기본값으로 그린다.
    /// </summary>
    public class CharacterAppearanceStore : IDisposable
    {
        private readonly IUserDataStore _users;
        private readonly Func<string, CancellationToken, UniTask<CharacterAppearanceResponse>> _fetch;
        private readonly ReactiveProperty<CharacterAppearance> _current = new(default);

        //  변경 응답을 반영할 때마다 올린다 — 그 전에 떠난 조회가 늦게 도착하면 낡은 값이라 버린다.
        private int _changes;

        public ReadOnlyReactiveProperty<CharacterAppearance> Current => _current;

        [VContainer.Inject]
        public CharacterAppearanceStore(IUserDataStore users) : this(users, WebAPI.GetCharacterAppearance)
        {
        }

        /// <param name="fetch">(userId, ct) → 내 외형 조회. 시험이 가짜를 꽂는다.</param>
        public CharacterAppearanceStore(IUserDataStore users, Func<string, CancellationToken, UniTask<CharacterAppearanceResponse>> fetch)
        {
            _users = users;
            _fetch = fetch ?? WebAPI.GetCharacterAppearance;
        }

        public async UniTask RefreshAsync(CancellationToken cancellationToken)
        {
            string userId = _users.user?.id;
            if (string.IsNullOrEmpty(userId)) return;

            int startedAt = _changes;
            try
            {
                var response = await _fetch(userId, cancellationToken);
                if (cancellationToken.IsCancellationRequested || _changes != startedAt) return;
                if (!TrySet(response))
                {
                    UnityEngine.Debug.LogWarning($"[CharacterAppearanceStore] Failed to refresh. code: {response?.code}");
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[CharacterAppearanceStore] Failed to refresh. error: {e.Message}");
            }
        }

        /// <summary>변경 응답 반영. 거절 응답이면 아무것도 안 바뀐다.</summary>
        public void Apply(CharacterAppearanceResponse response)
        {
            if (TrySet(response)) _changes++;
        }

        private bool TrySet(CharacterAppearanceResponse response)
        {
            if (response == null || response.code != ResponseCode.SUCCESS || response.characterAppearance == null) return false;
            _current.Value = new CharacterAppearance(response.characterAppearance.skinColorId, response.characterAppearance.hairColorId);
            return true;
        }

        public void Dispose() => _current.Dispose();
    }
}
