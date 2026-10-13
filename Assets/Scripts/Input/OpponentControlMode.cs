using UnityEngine;

/// <summary>
/// 相手キャラを「誰が操作するか」を、1つのドロップダウンで切り替えるスイッチ。
///
/// 【なぜこれが要るのか】
/// 切り替えに必要な操作が、CPUOpponentの Ai Enabled と HumanInputSource のチェックの
/// 2か所に分かれていると、片方だけ切り替えて「AIも止まっているし人間も操作できない」
/// という状態になりやすい。実際それで一度ハマったので、1か所にまとめた。
///
/// Modeを選ぶだけで、必要なコンポーネントのオン・オフが自動的に揃う。
/// 半端な状態になりようがないので、設定ミスで悩む余地がなくなる。
///
/// 【切り替わる中身】
/// ・CPU　　　　… CPUOpponentのAIが動く。HumanInputSourceは黙る（通常のテストマッチ）
/// ・LocalHuman … AIが止まり、同じ端末の2人目が操作する（キーボード or 画面の上半分）
/// ・Remote　　 … RemoteInputSourceが有効になり、ネット越しに届いた操作で動く
///
/// 「相手を誰が動かすか」の分岐がこのファイル1つに閉じているので、
/// 他のスクリプトを触らずに選択肢を増やせる。
///
/// 使い方：Enemyオブジェクトにアタッチする。参照は空欄でよい（同じオブジェクトから自動で探す）。
/// </summary>
public class OpponentControlMode : MonoBehaviour
{
    public enum Mode
    {
        CPU = 0,
        LocalHuman = 1,
        Remote = 2,
    }

    [Header("相手キャラを誰が操作するか")]
    [Tooltip("CPU＝AIが操作 / LocalHuman＝同じ端末の2人目が操作 / Remote＝ネット越しの相手")]
    public Mode mode = Mode.CPU;

    [Header("つなぐ相手（空欄なら同じオブジェクトから自動で探す）")]
    public CPUOpponent cpuOpponent;
    public HumanInputSource humanInput;
    public RemoteInputSource remoteInput;

    [Tooltip("自分側キャラの人間用入力。画面のどこを担当させるかを、モードに合わせて自動で切り替える")]
    public HumanInputSource playerInput;

    [Header("デバッグ")]
    [Tooltip("オンにすると、どのモードで起動したかをConsoleに出す")]
    public bool debugLog = true;

    void Awake()
    {
        if (cpuOpponent == null) { cpuOpponent = GetComponent<CPUOpponent>(); }
        if (humanInput == null) { humanInput = GetComponent<HumanInputSource>(); }
        if (remoteInput == null) { remoteInput = GetComponent<RemoteInputSource>(); }

        Apply();
    }

    /// <summary>選んだモードに合わせて、各コンポーネントのオン・オフを揃える</summary>
    public void Apply()
    {
        bool useCpu = mode == Mode.CPU;
        bool useLocalHuman = mode == Mode.LocalHuman;
        bool useRemote = mode == Mode.Remote;

        if (cpuOpponent != null)
        {
            cpuOpponent.aiEnabled = useCpu;
        }
        else if (useCpu)
        {
            Debug.LogWarning("[OpponentControlMode] " + gameObject.name +
                             " にCPUOpponentが無いので、CPUモードにできません");
        }

        if (humanInput != null)
        {
            humanInput.enabled = useLocalHuman;
        }
        else if (useLocalHuman)
        {
            Debug.LogWarning("[OpponentControlMode] " + gameObject.name +
                             " にHumanInputSourceが無いので、2人目が操作できません");
        }

        if (remoteInput != null)
        {
            remoteInput.enabled = useRemote;
        }
        else if (useRemote)
        {
            Debug.LogWarning("[OpponentControlMode] " + gameObject.name +
                             " にRemoteInputSourceが無いので、ネット越しの操作を受け取れません");
        }

        ApplyScreenRegions(useLocalHuman);

        if (debugLog)
        {
            Debug.Log("[OpponentControlMode] 相手キャラの操作＝" + mode +
                      "（AI:" + (useCpu ? "動く" : "停止") +
                      " / 2人目の入力:" + (useLocalHuman ? "有効" : "無効") +
                      " / ネット越し:" + (useRemote ? "有効" : "無効") + "）");
        }
    }

    /// <summary>
    /// タッチの担当範囲を、モードに合わせて振り分ける。
    ///
    /// 同じ端末で2人が遊ぶときだけ、画面を上下で分ける必要がある。
    /// それ以外（CPU戦・ネット対戦）では、自分が画面全体を使えないと
    /// 「上半分をフリックしても動かない」という状態になってしまう。
    ///
    /// この2つを別々に設定していると、片方だけ直し忘れて
    /// 「上半分を触ると相手が動く」ような噛み合わない状態になりやすいので、
    /// モードの切り替えとまとめて面倒を見るようにしてある。
    /// </summary>
    private void ApplyScreenRegions(bool splitScreen)
    {
        if (playerInput != null)
        {
            playerInput.screenRegion = splitScreen
                ? HumanInputSource.ScreenRegion.BottomHalf
                : HumanInputSource.ScreenRegion.Full;
        }

        if (humanInput != null)
        {
            humanInput.screenRegion = HumanInputSource.ScreenRegion.TopHalf;
        }
    }

    /// <summary>実行中にモードを変えたいときに呼ぶ</summary>
    public void SetMode(Mode next)
    {
        mode = next;
        Apply();
    }
}
