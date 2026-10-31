// ブラウザ版のセーブ置き場。
//
// 【なぜ PlayerPrefs のままではいけないのか】
// ブラウザ版の PlayerPrefs は IndexedDB に書かれるが、Unityはその保存先を
// ゲームのURLのパスから決めている。itch.io はzipを上げ直すたびにパスを変えるので、
// 中身は同じゲームでも、ブラウザからは「別のゲーム」に見えてしまう。
// つまり更新するたびに、遊んでいる人のレベルも名前も消える。
//
// localStorage はパスではなくドメインに紐づく。itch.io のゲームはどれも
// 同じドメインから配られているので、何度上げ直しても同じ置き場が見える。
//
// 【キーに tanktop. を付ける理由】
// そのドメインは itch.io のゲーム全部で共用している。"level" のような
// ありふれた名前を使うと、他のゲームの値と取り違える。必ず頭に付ける。
//
// 【throw を全部受け止める理由】
// プライベートブラウズや、保存を禁止した設定では、localStorage を触るだけで
// 例外が飛ぶ。ここで受け止めておかないと、ゲームがその場で止まってしまう。
// 保存できないなら、黙って「何も無かった」ことにして遊べるほうがいい。

mergeInto(LibraryManager.library, {

  TankTopSaveSetString: function (keyPtr, valuePtr) {
    try {
      window.localStorage.setItem(UTF8ToString(keyPtr), UTF8ToString(valuePtr));
    } catch (e) {
      // 保存できない設定。あきらめて続ける
    }
  },

  // C#側に文字を返すには、いったんメモリを確保して渡す。
  // 受け取った側が TankTopSaveFree で返しに来る約束になっている
  TankTopSaveGetString: function (keyPtr) {
    var value = null;
    try {
      value = window.localStorage.getItem(UTF8ToString(keyPtr));
    } catch (e) {
      value = null;
    }
    if (value === null) { value = ""; }

    var size = lengthBytesUTF8(value) + 1;
    var buffer = _malloc(size);
    stringToUTF8(value, buffer, size);
    return buffer;
  },

  TankTopSaveHasKey: function (keyPtr) {
    try {
      return window.localStorage.getItem(UTF8ToString(keyPtr)) !== null ? 1 : 0;
    } catch (e) {
      return 0;
    }
  },

  TankTopSaveDelete: function (keyPtr) {
    try {
      window.localStorage.removeItem(UTF8ToString(keyPtr));
    } catch (e) {
    }
  },

  TankTopSaveFree: function (ptr) {
    _free(ptr);
  }
});
