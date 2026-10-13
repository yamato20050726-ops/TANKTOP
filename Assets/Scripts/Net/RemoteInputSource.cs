using UnityEngine;

/// <summary>
/// ネットワーク越しに届いた相手の操作で、このキャラを動かす入力元。
///
/// HumanInputSource（フリックとキーボード）とまったく同じ立場で、
/// 違うのは「指示がどこから来るか」だけ。画面もキーボードも見ず、
/// MatchNetworkに届いたコマンドをそのままCharacterActionsに流す。
///
/// 相手側で「左へ動き出した」という出来事が起きたら、こちらでも同じように
/// 左へ動き出す。座標をそのまま受け取っているわけではないので、
/// 多少通信が遅れても、動きがカクついたり瞬間移動したりしにくい。
///
/// 使い方：Enemy（相手キャラ）にアタッチし、OpponentControlModeのModeをRemoteにする。
/// </summary>
public class RemoteInputSource : CharacterInputSource
{
    [Header("デバッグ")]
    [Tooltip("オンにすると、受信した操作をConsoleに出す")]
    public bool debugLog;

    private IMatchTransport subscribed;

    protected override void Awake()
    {
        base.Awake();
        MatchNetwork.TransportChanged += Resubscribe;
    }

    void OnEnable()
    {
        Resubscribe();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void OnDestroy()
    {
        MatchNetwork.TransportChanged -= Resubscribe;
        Unsubscribe();
    }

    /// <summary>
    /// 通信手段は、対戦の開始・終了で差し替わる。
    /// そのたびに購読をつなぎ直さないと、古い通信手段を見続けてしまう。
    /// </summary>
    private void Resubscribe()
    {
        Unsubscribe();

        if (!enabled)
        {
            return;
        }

        IMatchTransport transport = MatchNetwork.Current;
        if (transport == null)
        {
            return;
        }

        transport.CommandReceived += OnCommandReceived;
        subscribed = transport;
    }

    private void Unsubscribe()
    {
        if (subscribed == null)
        {
            return;
        }

        subscribed.CommandReceived -= OnCommandReceived;
        subscribed = null;
    }

    /// <summary>相手の操作が届いた</summary>
    private void OnCommandReceived(MatchCommand command)
    {
        if (debugLog)
        {
            Debug.Log("[RemoteInputSource] 受信 " + command);
        }

        switch (command)
        {
            case MatchCommand.MoveLeft: MoveLeft(); break;
            case MatchCommand.MoveRight: MoveRight(); break;
            // 届いた時点で相手はもう撃っている。こちらの都合で断らない
            case MatchCommand.Fire: FireFromRemote(); break;
            case MatchCommand.Skill2: UseSkillFromRemote(0); break;
            case MatchCommand.Skill3: UseSkillFromRemote(1); break;
        }
    }
}
