using UnityEngine;

/// <summary>
/// 自分の操作を、そのまま相手へ送る係。
///
/// CharacterActionsは「操作されたよ」という合図（CommandIssued）を出すので、
/// それを拾って通信へ流すだけ。自分のキャラの動きには一切干渉しないので、
/// 通信していないとき（テストマッチなど）は何もしないのと同じになる。
///
/// 「自分の操作を相手に送る」と「相手の操作で相手キャラを動かす」が、
/// このスクリプトとRemoteInputSourceの2つに分かれている。
/// どちらもCharacterActionsを介しているので、キャラ側のコードは
/// ネットワークのことを一切知らないままでいられる。
///
/// 使い方：Player（自分のキャラ）にアタッチする。
/// </summary>
[RequireComponent(typeof(CharacterActions))]
public class LocalCommandSender : MonoBehaviour
{
    [Header("送る相手（空欄なら同じオブジェクトから探す）")]
    public CharacterActions actions;

    [Header("デバッグ")]
    [Tooltip("オンにすると、送ろうとした操作をConsoleに出す。未接続かどうかも分かる")]
    public bool debugLog;

    void Awake()
    {
        if (actions == null)
        {
            actions = GetComponent<CharacterActions>();
        }
    }

    void OnEnable()
    {
        if (actions != null)
        {
            actions.CommandIssued += OnCommandIssued;
        }
    }

    void OnDisable()
    {
        if (actions != null)
        {
            actions.CommandIssued -= OnCommandIssued;
        }
    }

    private void OnCommandIssued(MatchCommand command)
    {
        IMatchTransport transport = MatchNetwork.Current;

        if (transport == null || !transport.IsConnected)
        {
            if (debugLog)
            {
                Debug.Log("[LocalCommandSender] " + command + " を送ろうとしたが、まだ未接続");
            }
            return;
        }

        transport.SendCommand(command);

        if (debugLog)
        {
            Debug.Log("[LocalCommandSender] 送信 " + command);
        }
    }
}
