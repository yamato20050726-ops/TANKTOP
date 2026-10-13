using UnityEngine;

/// <summary>
/// キャラに対して「できること」だけを並べた、操作の差込口。
///
/// このゲームでキャラにできる操作は、突きつめると4つしかない。
/// ・左へ動き出す ・右へ動き出す ・ショットを撃つ ・スキルを使う
///
/// 今までは、この4つを「誰が指示するか」がキャラ本体に直接埋め込まれていた。
/// Playerはタッチ操作、Enemyは CPUOpponent のAI、というふうに固定されていたので、
/// 相手を人間やネットワーク越しのプレイヤーに差し替えることができなかった。
///
/// そこで、操作する側とされる側のあいだにこのクラスを1枚挟む。
/// 操作する側（CharacterInputSource）は、相手がAIだろうが人間だろうが
/// ネットワーク越しだろうが、このクラスの4つのメソッドを呼ぶだけでよくなる。
/// 逆にキャラ側は、誰に呼ばれたのかを気にしなくてよくなる。
///
/// PlayerとEnemyでは中身の部品が違う（PlayerMovement/PlayerShooter か CPUOpponent か）が、
/// その違いもこのクラスが吸収するので、外から見れば両者は同じ顔をしている。
///
/// ネットワーク対戦を実装するときは、相手キャラのInput SourceをRemoteInputSourceに
/// 差し替えて、受信したパケットからこの4つを呼ぶだけで済む。
/// 逆に「自分の操作を相手に送る」のも、ここを通る内容をそのまま送ればよい。
///
/// 使い方：PlayerとEnemyの両方にアタッチする。参照は空欄でよい（同じオブジェクトから自動で探す）。
/// </summary>
public class CharacterActions : MonoBehaviour
{
    [Header("つなぐ相手（空欄なら同じオブジェクトから自動で探す）")]
    public PlayerMovement movement;
    public PlayerShooter shooter;
    public CPUOpponent cpuOpponent;
    public SkillManager skillManager;

    [Header("デバッグ")]
    [Tooltip("オンにすると、呼ばれた操作をConsoleに出す。ネットワーク実装時の確認用")]
    public bool debugLog;

    /// <summary>
    /// 操作が入ったときに鳴る合図。LocalCommandSenderがこれを拾って相手へ送る。
    ///
    /// 「送信する」という仕事をここに書かずに合図だけ出しているのは、
    /// このクラスにネットワークのことを知らせないため。通信していないときは
    /// 誰も聞いていないだけなので、何の副作用もない。
    /// </summary>
    public event System.Action<MatchCommand> CommandIssued;

    void Awake()
    {
        if (movement == null) { movement = GetComponent<PlayerMovement>(); }
        if (shooter == null) { shooter = GetComponent<PlayerShooter>(); }
        if (cpuOpponent == null) { cpuOpponent = GetComponent<CPUOpponent>(); }
        if (skillManager == null) { skillManager = GetComponent<SkillManager>(); }
    }

    /// <summary>左へ動き出す（壁に着くか、右への指示があるまで動き続ける）</summary>
    public void MoveLeft()
    {
        Log("MoveLeft");
        Raise(MatchCommand.MoveLeft);

        if (movement != null) { movement.MoveLeft(); }
        if (cpuOpponent != null) { cpuOpponent.MoveLeft(); }
    }

    /// <summary>右へ動き出す（壁に着くか、左への指示があるまで動き続ける）</summary>
    public void MoveRight()
    {
        Log("MoveRight");
        Raise(MatchCommand.MoveRight);

        if (movement != null) { movement.MoveRight(); }
        if (cpuOpponent != null) { cpuOpponent.MoveRight(); }
    }

    /// <summary>
    /// 通常ショットを撃つ。クールダウン中やポイント不足のときは、
    /// 呼ばれた側が黙って何もしない（判断は各コンポーネントに任せる）。
    /// </summary>
    public void Fire()
    {
        Log("Fire");

        // 【送るのは、実際に撃てたときだけ】
        // ここで先に送ってしまうと、連射制限中やポイント不足で
        // 自分の画面では何も起きなかったぶんまで相手に届く。
        // 相手側は「もう撃った」という報告として必ず弾を出すので、
        // 撃てないあいだ連打したぶんだけ、相手の画面にだけ弾が湧き続ける。
        // しかも撃てなかったときはクールダウンも進まないので、際限がなくなる。
        bool fired = false;

        if (shooter != null) { fired |= shooter.TryFire(); }
        if (cpuOpponent != null) { fired |= cpuOpponent.TryFire(); }

        if (fired) { Raise(MatchCommand.Fire); }
    }

    /// <summary>
    /// 通信で届いた「相手が撃った」を再現する。
    ///
    /// 【なぜ Fire() と分けるのか】
    /// Fire() は「撃ちたい」という要求で、撃てるかどうかは呼ばれた側が決める。
    /// こちらは「相手はもう撃った」という報告なので、断る余地がない。
    /// 同じ入口にすると、こちらの帳簿の都合で相手の弾を消してしまう。
    ///
    /// 送り返しの合図も出さない。届いたものをそのまま送り返すと輪になる。
    /// </summary>
    public void FireFromRemote()
    {
        Log("Fire (remote)");

        if (shooter != null) { shooter.FireFromRemote(); }
        if (cpuOpponent != null) { cpuOpponent.FireFromRemote(); }
    }

    /// <summary>通信で届いた「相手がスキルを使った」を再現する</summary>
    public void UseSkillFromRemote(int slotIndex)
    {
        Log("UseSkill (remote) " + slotIndex);

        if (skillManager != null) { skillManager.ActivateFromRemote(slotIndex); }
    }

    /// <summary>
    /// その場で止める。試合の開始時に呼んで、メニュー操作の名残で
    /// 走り出したままになっているのを打ち消すために使う。
    /// これは自分の都合の初期化なので、相手には送らない。
    /// </summary>
    public void StopMoving()
    {
        if (movement != null) { movement.StopMoving(); }
        if (cpuOpponent != null) { cpuOpponent.StopMoving(); }
    }

    /// <summary>スキルを使う。slotIndexは スキル2=0、スキル3=1。</summary>
    public void UseSkill(int slotIndex)
    {
        Log("UseSkill " + slotIndex);

        // ショットと同じ理由で、実際に発動できたときだけ送る
        bool used = skillManager != null && skillManager.TryActivateSkill(slotIndex);

        if (used)
        {
            Raise(slotIndex == 0 ? MatchCommand.Skill2 : MatchCommand.Skill3);
        }
    }

    private void Raise(MatchCommand command)
    {
        if (CommandIssued != null)
        {
            CommandIssued(command);
        }
    }

    private void Log(string action)
    {
        if (debugLog)
        {
            Debug.Log("[CharacterActions] " + gameObject.name + " → " + action);
        }
    }
}
