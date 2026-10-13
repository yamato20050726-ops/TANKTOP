using UnityEngine;

/// <summary>
/// プレイヤー・CPU共通のHP管理コンポーネント。
///
/// 現在の仕様では「無敵でなければ被弾1発で敗北」という即死ルールなので、
/// HPの残量そのものはほぼ使っていないが、将来ルールを変えたときのために
/// maxHp / currentHp は残してある。
///
/// 倒れたときは Died イベントを発火する。MatchManagerがこれを受け取って
/// 勝敗を判定し、リザルト画面を出す。
///
/// 使い方：Player/CPU両方のオブジェクトにアタッチする。
/// </summary>
public class CharacterHealth : MonoBehaviour
{
    [Tooltip("最大HP")]
    public int maxHp = 100;

    [HideInInspector] public int currentHp;

    [Tooltip("trueの間は被弾ダメージを無効化する（シールドなどの効果で使用）")]
    public bool isInvulnerable = false;

    /// <summary>倒れた瞬間に一度だけ呼ばれる。引数は倒れた本人。</summary>
    public event System.Action<CharacterHealth> Died;

    private bool isDead;

    /// <summary>すでに倒れているかどうか</summary>
    public bool IsDead => isDead;

    void Awake()
    {
        ResetHealth();
    }

    /// <summary>
    /// HPと死亡フラグを開始状態に戻す。
    /// キャラを切り替えて maxHp を変更したあとや、試合をやり直すときに呼ぶ。
    /// </summary>
    public void ResetHealth()
    {
        currentHp = maxHp;
        isDead = false;
        isInvulnerable = false;
    }

    /// <summary>
    /// 被弾処理。仕様：HPの残量に関係なく、無敵でなければ1発で敗北する（即死）。
    /// amount引数は将来仕様が変わった場合のために残してあるが、現状は使用していない。
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (isInvulnerable || isDead)
        {
            return;
        }

        // 対戦中は、当たったかどうかの判断はホストだけが行う。
        // 弾はお互いの端末で別々に飛んでいるので、遅延のぶん位置がわずかにズレる。
        // 各自で判定すると「片方の画面では当たった、もう片方では外れた」となり、
        // 勝敗そのものが食い違ってしまう。
        // ゲストはホストからの連絡（Defeated）を待つ。
        if (!MatchSides.IsAuthority)
        {
            return;
        }

        if (MatchSides.IsNetworked && MatchNetwork.Current != null)
        {
            MatchNetwork.Current.SendSignal(MatchSignal.Defeated, MatchSides.SideOf(gameObject));
        }

        currentHp = 0;
        Die();
    }

    /// <summary>ホストから「この人が倒れた」と届いたときの処理</summary>
    public void ApplyRemoteDefeat()
    {
        if (isDead)
        {
            return;
        }

        currentHp = 0;
        Die();
    }

    private void Die()
    {
        isDead = true;

        // ホストが自分で判定したときも、ゲストがホストの連絡で倒れたときも、
        // かならずここを通る。片方だけ無音になる心配がない
        GameAudio.Defeat();

        Debug.Log($"{gameObject.name} は倒れた！");

        if (Died != null)
        {
            Died(this);
        }
    }
}
