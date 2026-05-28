#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RiccaLab {
    // シーン内で使用中のComponentを複数条件で検索するEditorWindow
    public class ComponentFinder : EditorWindow {
        // 表示文言をまとめる定数
        private static class Text {
            public const string MenuPath = "Tools/RiccaLab/Finder";
            public const string WindowTitle = "Finder";

            public const string TargetLabel = "Target";
            public const string ComponentLabel = "Search";
            public const string HistoryLabel = "History";
            public const string ResultsLabel = "Result";

            public const string ClearButton = "Clear";
            public const string RemoveButton = "Remove";
            public const string AllButton = "All";
            public const string NoneButton = "None";
            public const string ApplyButton = "Apply";
            public const string CancelButton = "Cancel";
            public const string ComponentSelectButton = "";

            public const string NoSelectedComponents = "コンポーネントが選択されていません";
            public const string SelectComponentMessage = "検索するコンポーネントを選択してください。";
            public const string NoResultsMessage = "該当するコンポーネントは見つかりませんでした。";

            public const string SelectTooltip = "選択";
            public const string PingTooltip = "ピン留め";
            public const string AllPingTooltip = "全てピン留め";
            public const string ClearPingTooltip = "ピン留めを解除";

            public const string SelectFallback = "S";
            public const string PingFallback = "P";
            public const string AllPingFallback = "A";
            public const string ClearPingFallback = "C";

            public const string ComponentDropdownTitle = "";
            public const string ComponentSearchControlName = "ComponentFinderSearchField";
        }

        // レイアウト値をまとめる定数
        private static class Layout {
            public const float LabelWidth = 90f;
            public const float SmallButtonWidth = 60f;
            public const float RemoveButtonWidth = 70f;
            public const float IconButtonWidth = 24f;
            public const float ButtonHeight = 22f;
            public const float FooterButtonWidth = 90f;
            public const float FooterButtonHeight = 24f;
            public const float SelectedComponentMaxHeight = 110f;
            public const float SelectedComponentRowHeight = 24f;
            public const float WindowSpaceTiny = 2f;
            public const float WindowSpaceSmall = 4f;
            public const float WindowSpaceMedium = 6f;

            public const float DropdownWindowWidth = 540f;
            public const float DropdownWindowHeight = 540f;
            public const float DropdownRowHeight = 22f;
            public const float DropdownToggleSize = 18f;
            public const float DropdownIconSize = 16f;
            public const float DropdownNameOffset = 48f;
            public const float DropdownFullNameOffset = 250f;

            public const float DropdownRowToggleX = 4f;
            public const float DropdownRowToggleY = 2f;
            public const float DropdownRowIconX = 26f;
            public const float DropdownRowIconY = 3f;
            public const float DropdownRowRightPadding = 4f;
            public const float DropdownNamePadding = 8f;
        }

        // Unity標準アイコン名をまとめる定数
        private static class IconNames {
            public static readonly string[] Select = {
                "d_GameObject Icon",
                "GameObject Icon",
                "d_ToolHandleCenter",
                "ToolHandleCenter",
                "d_UnityEditor.HierarchyWindow",
                "UnityEditor.HierarchyWindow"
            };

            public static readonly string[] Ping = {
                "d_scenepicking_pickable_hover",
                "scenepicking_pickable_hover"
            };

            public static readonly string[] AllPing = {
                "d_ToggleUVOverlay",
                "ToggleUVOverlay",
                "d_FilterByType",
                "FilterByType",
                "d_Grid.BoxTool",
                "Grid.BoxTool"
            };

            public static readonly string[] ClearPing = {
                "d_TreeEditor.Trash",
                "TreeEditor.Trash",
                "d_P4_DeletedLocal",
                "P4_DeletedLocal",
                "d_clear",
                "clear"
            };

            public const string FallbackScript = "cs Script Icon";
        }

        // 履歴保存用のEditorPrefsキー
        private const string HistoryPrefsKey = "RiccaLab.ComponentFinder.ComponentHistory";

        // 履歴の最大保存件数
        private const int MaxHistoryCount = 20;

        // 履歴ComboBoxの初期表示Index
        private const int HistoryDefaultIndex = 0;

        // 検索対象のGameObject
        private GameObject targetObject;

        // 履歴ComboBoxの選択Index
        private int historyPopupIndex = HistoryDefaultIndex;

        // 選択変更の自己反応を抑制するかどうか
        private bool suppressSelectionChanged;

        // Ping選択の反映予約中かどうか
        private bool isSelectionApplyScheduled;

        // 予約反映時にPing表示するかどうか
        private bool shouldPingOnScheduledApply;

        // 検索結果のスクロール位置
        private Vector2 resultScroll;

        // 選択Component一覧のスクロール位置
        private Vector2 selectedComponentScroll;

        // 検索対象のComponent型一覧
        private readonly List<Type> selectedComponentTypes = new List<Type>();

        // 検索履歴のComponent型一覧
        private readonly List<ComponentTypeItem> componentHistory = new List<ComponentTypeItem>();

        // 検索結果のComponent一覧
        private readonly List<Component> results = new List<Component>();

        // Ping選択中GameObjectの表示順を保持するInstanceID一覧
        private readonly List<int> pingSelectedInstanceIds = new List<int>();

        // Ping選択中GameObjectの重複判定を高速化するInstanceID集合
        private readonly HashSet<int> pingSelectedInstanceIdSet = new HashSet<int>();

        // 最後にPingしたGameObjectのInstanceID
        private int lastPingInstanceId;

        // ウィンドウを開く処理
        [MenuItem(Text.MenuPath)]
        private static void Open() {
            GetWindow<ComponentFinder>(Text.WindowTitle);
        }

        // ウィンドウ有効化時の初期化処理
        private void OnEnable() {
            LoadHistory();
            Selection.selectionChanged -= OnUnitySelectionChanged;
            Selection.selectionChanged += OnUnitySelectionChanged;
        }

        // ウィンドウ無効化時の後処理
        private void OnDisable() {
            Selection.selectionChanged -= OnUnitySelectionChanged;
        }

        // ウィンドウ描画処理
        private void OnGUI() {
            DrawTargetSelector();
            DrawComponentSelector();
            DrawHistoryComboBox();
            DrawSelectedComponents();
            DrawResultHeader();
            DrawResults();
        }

        // 検索対象Target選択欄を描画する処理
        private void DrawTargetSelector() {
            EditorGUILayout.Space(Layout.WindowSpaceMedium);
            EditorGUI.BeginChangeCheck();

            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField(Text.TargetLabel, GUILayout.Width(Layout.LabelWidth));
                targetObject = EditorGUILayout.ObjectField(targetObject, typeof(GameObject), true) as GameObject;

                if (GUILayout.Button(Text.ClearButton, GUILayout.Width(Layout.SmallButtonWidth), GUILayout.Height(Layout.ButtonHeight))) {
                    targetObject = null;
                    Search();
                    GUIUtility.ExitGUI();
                }
            }

            if (EditorGUI.EndChangeCheck()) {
                Search();
            }
        }

        // Component選択欄を描画する処理
        private void DrawComponentSelector() {
            EditorGUILayout.Space(Layout.WindowSpaceMedium);

            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField(Text.ComponentLabel, GUILayout.Width(Layout.LabelWidth));

                if (GUILayout.Button(Text.ComponentSelectButton, EditorStyles.popup, GUILayout.Height(Layout.ButtonHeight))) {
                    Rect rect = GUILayoutUtility.GetLastRect();

                    ComponentTypeDropdown.Show(rect, selectedComponentTypes, targetObject, types => {
                        SetSelectedComponents(types);
                        Search();
                    });
                }

                if (GUILayout.Button(Text.ClearButton, GUILayout.Width(Layout.SmallButtonWidth), GUILayout.Height(Layout.ButtonHeight))) {
                    selectedComponentTypes.Clear();
                    results.Clear();
                    Repaint();
                    GUIUtility.ExitGUI();
                }
            }
        }

        // 検索履歴をComboBoxとして描画する処理
        private void DrawHistoryComboBox() {
            EditorGUILayout.Space(Layout.WindowSpaceSmall);

            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField(Text.HistoryLabel, GUILayout.Width(Layout.LabelWidth));

                List<ComponentTypeItem> validHistory = GetValidHistoryItems();

                if (validHistory.Count == 0) {
                    DrawDisabledHistoryComboBox();
                    return;
                }

                List<string> options = new List<string> { string.Empty };
                options.AddRange(validHistory.Select(item => item.DisplayName));

                EditorGUI.BeginChangeCheck();
                historyPopupIndex = EditorGUILayout.Popup(historyPopupIndex, options.ToArray());

                if (EditorGUI.EndChangeCheck()) {
                    ApplySelectedHistory(validHistory);
                }

                if (GUILayout.Button(Text.ClearButton, GUILayout.Width(Layout.SmallButtonWidth), GUILayout.Height(Layout.ButtonHeight))) {
                    ClearHistory();
                    GUIUtility.ExitGUI();
                }
            }
        }

        // 有効な履歴項目のみを取得する処理
        private List<ComponentTypeItem> GetValidHistoryItems() {
            return componentHistory
                .Where(item => item != null && item.Type != null)
                .ToList();
        }

        // 履歴が空のときの無効化ComboBoxを描画する処理
        private void DrawDisabledHistoryComboBox() {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Popup(HistoryDefaultIndex, new[] { string.Empty });
            GUILayout.Button(Text.ClearButton, GUILayout.Width(Layout.SmallButtonWidth), GUILayout.Height(Layout.ButtonHeight));
            EditorGUI.EndDisabledGroup();
        }

        // 履歴ComboBoxで選択したComponent型を検索対象へ反映する処理
        private void ApplySelectedHistory(List<ComponentTypeItem> validHistory) {
            if (historyPopupIndex > 0) {
                int selectedIndex = historyPopupIndex - 1;

                if (selectedIndex >= 0 && selectedIndex < validHistory.Count) {
                    AddSelectedComponent(validHistory[selectedIndex].Type);
                    Search();
                }
            }

            historyPopupIndex = HistoryDefaultIndex;
        }

        // 選択中のComponent一覧を描画する処理
        private void DrawSelectedComponents() {
            EditorGUILayout.Space(Layout.WindowSpaceSmall);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                RemoveInvalidSelectedComponentTypes();

                if (selectedComponentTypes.Count == 0) {
                    EditorGUILayout.LabelField(Text.NoSelectedComponents);
                    return;
                }

                float height = Mathf.Min(Layout.SelectedComponentMaxHeight, selectedComponentTypes.Count * Layout.SelectedComponentRowHeight + Layout.WindowSpaceMedium);
                selectedComponentScroll = EditorGUILayout.BeginScrollView(selectedComponentScroll, GUILayout.Height(height));

                for (int i = selectedComponentTypes.Count - 1; i >= 0; i--) {
                    DrawSelectedComponentRow(i);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        // 無効なComponent型を選択一覧から削除する処理
        private void RemoveInvalidSelectedComponentTypes() {
            selectedComponentTypes.RemoveAll(type => type == null || !typeof(Component).IsAssignableFrom(type));
        }

        // 選択中Componentの1行表示を描画する処理
        private void DrawSelectedComponentRow(int index) {
            Type type = selectedComponentTypes[index];

            using (new EditorGUILayout.HorizontalScope()) {
                Texture icon = GetComponentIcon(type);
                EditorGUILayout.LabelField(new GUIContent(ObjectNames.NicifyVariableName(type.Name), icon), GUILayout.MinWidth(160));
                EditorGUILayout.LabelField(type.FullName, EditorStyles.miniLabel);

                if (GUILayout.Button(Text.RemoveButton, GUILayout.Width(Layout.RemoveButtonWidth))) {
                    selectedComponentTypes.RemoveAt(index);
                    Search();
                    GUIUtility.ExitGUI();
                }
            }
        }

        // 検索結果の見出しを描画する処理
        private void DrawResultHeader() {
            EditorGUILayout.Space(Layout.WindowSpaceMedium);

            GUIContent allIcon = GetEditorIconContent(IconNames.AllPing, Text.AllPingFallback, Text.AllPingTooltip);
            GUIContent clearIcon = GetEditorIconContent(IconNames.ClearPing, Text.ClearPingFallback, Text.ClearPingTooltip);

            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField(Text.ResultsLabel);

                EditorGUI.BeginDisabledGroup(pingSelectedInstanceIds.Count == 0);

                if (GUILayout.Button(clearIcon, GUILayout.Width(Layout.IconButtonWidth), GUILayout.Height(Layout.ButtonHeight))) {
                    ClearPingSelection();
                    GUIUtility.ExitGUI();
                }

                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(results.Count == 0);

                if (GUILayout.Button(allIcon, GUILayout.Width(Layout.IconButtonWidth), GUILayout.Height(Layout.ButtonHeight))) {
                    AddAllResultsToPingSelection();
                    GUIUtility.ExitGUI();
                }

                EditorGUI.EndDisabledGroup();
            }
        }

        // 検索結果一覧を描画する処理
        private void DrawResults() {
            EditorGUILayout.Space(Layout.WindowSpaceTiny);
            resultScroll = EditorGUILayout.BeginScrollView(resultScroll);

            if (selectedComponentTypes.Count == 0) {
                EditorGUILayout.HelpBox(Text.SelectComponentMessage, MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            if (results.Count == 0) {
                EditorGUILayout.HelpBox(Text.NoResultsMessage, MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            GUIContent selectIcon = GetEditorIconContent(IconNames.Select, Text.SelectFallback, Text.SelectTooltip);
            GUIContent pingIcon = GetEditorIconContent(IconNames.Ping, Text.PingFallback, Text.PingTooltip);

            foreach (Component component in results.ToArray()) {
                if (component == null || component.gameObject == null) {
                    continue;
                }

                DrawResultRow(component, selectIcon, pingIcon);
            }

            EditorGUILayout.EndScrollView();
        }

        // 検索結果の1行表示を描画する処理
        private void DrawResultRow(Component component, GUIContent selectIcon, GUIContent pingIcon) {
            GameObject resultObject = component.gameObject;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField(component, typeof(Component), true);
                EditorGUI.EndDisabledGroup();

                if (GUILayout.Button(selectIcon, GUILayout.Width(Layout.IconButtonWidth), GUILayout.Height(Layout.ButtonHeight))) {
                    SelectSingleGameObject(resultObject);
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button(pingIcon, GUILayout.Width(Layout.IconButtonWidth), GUILayout.Height(Layout.ButtonHeight))) {
                    AddPingSelection(resultObject);
                    GUIUtility.ExitGUI();
                }
            }
        }

        // Unity側の選択変更時にPing選択を復元する処理
        private void OnUnitySelectionChanged() {
            if (suppressSelectionChanged || pingSelectedInstanceIds.Count == 0 || IsCurrentSelectionSameAsPingSelection()) {
                return;
            }

            ScheduleApplyPingSelection(false);
        }

        // GameObjectだけを単体選択してHierarchy上の選択状態にする処理
        private void SelectSingleGameObject(GameObject obj) {
            if (obj == null) {
                return;
            }

            ClearPingSelectionCache();

            suppressSelectionChanged = true;
            Selection.objects = new UnityEngine.Object[] { obj };
            suppressSelectionChanged = false;

            EditorGUIUtility.PingObject(obj);
            EditorApplication.RepaintHierarchyWindow();
            Repaint();
        }

        // PingしたGameObjectをClearするまで選択対象として追加する処理
        private void AddPingSelection(GameObject obj) {
            if (obj == null) {
                return;
            }

            int instanceId = obj.GetInstanceID();

            if (pingSelectedInstanceIdSet.Add(instanceId)) {
                pingSelectedInstanceIds.Add(instanceId);
            }

            lastPingInstanceId = instanceId;
            ScheduleApplyPingSelection(true);
        }

        // 検索結果のGameObjectを一括でPing選択へ追加する処理
        private void AddAllResultsToPingSelection() {
            GameObject lastAddedObject = null;

            foreach (Component component in results) {
                if (component == null || component.gameObject == null) {
                    continue;
                }

                GameObject obj = component.gameObject;
                int instanceId = obj.GetInstanceID();

                if (pingSelectedInstanceIdSet.Add(instanceId)) {
                    pingSelectedInstanceIds.Add(instanceId);
                    lastAddedObject = obj;
                }
            }

            if (lastAddedObject == null && pingSelectedInstanceIds.Count > 0) {
                lastAddedObject = EditorUtility.InstanceIDToObject(pingSelectedInstanceIds[pingSelectedInstanceIds.Count - 1]) as GameObject;
            }

            if (lastAddedObject == null) {
                return;
            }

            lastPingInstanceId = lastAddedObject.GetInstanceID();
            ScheduleApplyPingSelection(true);
        }

        // Ping選択状態を解除する処理
        private void ClearPingSelection() {
            ClearPingSelectionCache();

            suppressSelectionChanged = true;
            Selection.objects = new UnityEngine.Object[0];
            suppressSelectionChanged = false;

            EditorApplication.RepaintHierarchyWindow();
            Repaint();
        }

        // Ping選択の内部保持情報を初期化する処理
        private void ClearPingSelectionCache() {
            pingSelectedInstanceIds.Clear();
            pingSelectedInstanceIdSet.Clear();
            lastPingInstanceId = 0;
        }

        // Ping選択反映を予約する処理
        private void ScheduleApplyPingSelection(bool pingLastObject) {
            if (pingLastObject) {
                shouldPingOnScheduledApply = true;
            }

            if (isSelectionApplyScheduled) {
                return;
            }

            isSelectionApplyScheduled = true;

            EditorApplication.delayCall += () => {
                isSelectionApplyScheduled = false;

                if (this == null) {
                    return;
                }

                bool shouldPing = shouldPingOnScheduledApply;
                shouldPingOnScheduledApply = false;

                ApplyPingSelection(shouldPing);
            };
        }

        // Ping選択中Objectを現在のSelectionへ反映する処理
        private void ApplyPingSelection(bool pingLastObject) {
            List<GameObject> validObjects = GetValidPingSelectedObjects();

            if (validObjects.Count == 0) {
                ClearPingSelectionCache();
                EditorApplication.RepaintHierarchyWindow();
                Repaint();
                return;
            }

            GameObject lastObject = GetValidLastPingObject(validObjects);
            List<UnityEngine.Object> orderedSelection = BuildOrderedSelection(validObjects, lastObject);

            suppressSelectionChanged = true;
            Selection.objects = orderedSelection.ToArray();
            suppressSelectionChanged = false;

            if (pingLastObject) {
                EditorGUIUtility.PingObject(lastObject);
            }

            EditorApplication.RepaintHierarchyWindow();
            Repaint();

            EditorApplication.delayCall += () => {
                if (this == null) {
                    return;
                }

                EditorApplication.RepaintHierarchyWindow();
                Repaint();
            };
        }

        // 最後にPingした有効なGameObjectを取得する処理
        private GameObject GetValidLastPingObject(List<GameObject> validObjects) {
            GameObject lastObject = EditorUtility.InstanceIDToObject(lastPingInstanceId) as GameObject;

            if (lastObject != null && validObjects.Contains(lastObject)) {
                return lastObject;
            }

            lastObject = validObjects[validObjects.Count - 1];
            lastPingInstanceId = lastObject.GetInstanceID();
            return lastObject;
        }

        // 最後にPingしたObjectを末尾にしたSelection一覧を構築する処理
        private static List<UnityEngine.Object> BuildOrderedSelection(List<GameObject> validObjects, GameObject lastObject) {
            List<UnityEngine.Object> orderedSelection = new List<UnityEngine.Object>();

            foreach (GameObject obj in validObjects) {
                if (obj != null && obj != lastObject) {
                    orderedSelection.Add(obj);
                }
            }

            if (lastObject != null) {
                orderedSelection.Add(lastObject);
            }

            return orderedSelection;
        }

        // 現在のSelectionがPing対象と完全一致しているか判定する処理
        private bool IsCurrentSelectionSameAsPingSelection() {
            RemoveInvalidPingSelectionIds();

            if (Selection.objects.Length != pingSelectedInstanceIds.Count) {
                return false;
            }

            HashSet<int> selectedIds = new HashSet<int>();

            foreach (UnityEngine.Object selectedObject in Selection.objects) {
                GameObject selectedGameObject = selectedObject as GameObject;

                if (selectedGameObject == null) {
                    return false;
                }

                selectedIds.Add(selectedGameObject.GetInstanceID());
            }

            return pingSelectedInstanceIdSet.SetEquals(selectedIds);
        }

        // Ping選択中の有効なGameObject一覧を取得する処理
        private List<GameObject> GetValidPingSelectedObjects() {
            RemoveInvalidPingSelectionIds();

            List<GameObject> objects = new List<GameObject>();

            foreach (int instanceId in pingSelectedInstanceIds) {
                GameObject obj = EditorUtility.InstanceIDToObject(instanceId) as GameObject;

                if (obj != null) {
                    objects.Add(obj);
                }
            }

            return objects;
        }

        // 無効なPing選択IDを削除する処理
        private void RemoveInvalidPingSelectionIds() {
            for (int i = pingSelectedInstanceIds.Count - 1; i >= 0; i--) {
                int instanceId = pingSelectedInstanceIds[i];

                if (EditorUtility.InstanceIDToObject(instanceId) != null) {
                    continue;
                }

                pingSelectedInstanceIds.RemoveAt(i);
                pingSelectedInstanceIdSet.Remove(instanceId);

                if (lastPingInstanceId == instanceId) {
                    lastPingInstanceId = 0;
                }
            }
        }

        // Component型一覧を検索対象として設定する処理
        private void SetSelectedComponents(List<Type> types) {
            selectedComponentTypes.Clear();

            if (types == null) {
                return;
            }

            foreach (Type type in types) {
                AddSelectedComponent(type);
            }
        }

        // Component型を検索対象へ追加する処理
        private void AddSelectedComponent(Type type) {
            if (type == null || !typeof(Component).IsAssignableFrom(type)) {
                return;
            }

            if (!selectedComponentTypes.Contains(type)) {
                selectedComponentTypes.Add(type);
            }

            AddHistory(type);
        }

        // Hierarchy内から指定Componentを検索する処理
        private void Search() {
            results.Clear();

            if (selectedComponentTypes.Count == 0) {
                Repaint();
                return;
            }

            foreach (GameObject obj in GetSearchTargetObjects(targetObject)) {
                if (obj == null) {
                    continue;
                }

                Component[] components = obj.GetComponents<Component>();

                foreach (Component component in components) {
                    if (component != null && MatchesSelectedTypes(component.GetType())) {
                        results.Add(component);
                    }
                }
            }

            SortResults();
            Repaint();
        }

        // 検索結果をHierarchyパスとComponent型名で並び替える処理
        private void SortResults() {
            results.Sort((a, b) => {
                if (a == null || b == null) {
                    return a == b ? 0 : a == null ? 1 : -1;
                }

                int pathCompare = string.Compare(GetPath(a.transform), GetPath(b.transform), StringComparison.OrdinalIgnoreCase);

                if (pathCompare != 0) {
                    return pathCompare;
                }

                return string.Compare(a.GetType().FullName, b.GetType().FullName, StringComparison.OrdinalIgnoreCase);
            });
        }

        // Component型が検索対象に一致するか判定する処理
        private bool MatchesSelectedTypes(Type componentType) {
            if (componentType == null) {
                return false;
            }

            foreach (Type selectedType in selectedComponentTypes) {
                if (selectedType != null && selectedType.IsAssignableFrom(componentType)) {
                    return true;
                }
            }

            return false;
        }

        // 検索対象のGameObject一覧を取得する処理
        private static List<GameObject> GetSearchTargetObjects(GameObject targetObject) {
            return targetObject == null ? GetSceneObjects() : GetChildObjects(targetObject);
        }

        // シーン内に存在するGameObjectを取得する処理
        private static List<GameObject> GetSceneObjects() {
            List<GameObject> objects = new List<GameObject>();
            GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

            foreach (GameObject rootObject in rootObjects) {
                CollectHierarchyObjects(rootObject, objects);
            }

            return objects;
        }

        // 指定Targetの子GameObjectを取得する処理
        private static List<GameObject> GetChildObjects(GameObject targetObject) {
            List<GameObject> objects = new List<GameObject>();

            if (targetObject == null) {
                return objects;
            }

            Transform targetTransform = targetObject.transform;

            for (int i = 0; i < targetTransform.childCount; i++) {
                Transform child = targetTransform.GetChild(i);

                if (child != null) {
                    CollectHierarchyObjects(child.gameObject, objects);
                }
            }

            return objects;
        }

        // GameObjectと子階層をHierarchy検索対象として収集する処理
        private static void CollectHierarchyObjects(GameObject obj, List<GameObject> objects) {
            if (obj == null) {
                return;
            }

            objects.Add(obj);

            Transform targetTransform = obj.transform;

            for (int i = 0; i < targetTransform.childCount; i++) {
                Transform child = targetTransform.GetChild(i);

                if (child != null) {
                    CollectHierarchyObjects(child.gameObject, objects);
                }
            }
        }

        // TransformのHierarchyパスを取得する処理
        private static string GetPath(Transform targetTransform) {
            if (targetTransform == null) {
                return string.Empty;
            }

            Stack<string> names = new Stack<string>();
            Transform current = targetTransform;

            while (current != null) {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names.ToArray());
        }

        // Component型を履歴へ追加する処理
        private void AddHistory(Type type) {
            if (type == null) {
                return;
            }

            componentHistory.RemoveAll(item => item == null || item.Type == null || item.Type == type);
            componentHistory.Insert(0, new ComponentTypeItem(type));

            while (componentHistory.Count > MaxHistoryCount) {
                componentHistory.RemoveAt(componentHistory.Count - 1);
            }

            SaveHistory();
        }

        // 検索履歴を削除する処理
        private void ClearHistory() {
            componentHistory.Clear();
            historyPopupIndex = HistoryDefaultIndex;
            EditorPrefs.DeleteKey(HistoryPrefsKey);
            Repaint();
        }

        // 保存済み履歴を読み込む処理
        private void LoadHistory() {
            componentHistory.Clear();

            string value = EditorPrefs.GetString(HistoryPrefsKey, string.Empty);

            if (string.IsNullOrEmpty(value)) {
                return;
            }

            string[] names = value.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string name in names) {
                Type type = Type.GetType(name);

                if (type != null && typeof(Component).IsAssignableFrom(type)) {
                    componentHistory.Add(new ComponentTypeItem(type));
                }
            }
        }

        // 検索履歴をEditorPrefsへ保存する処理
        private void SaveHistory() {
            StringBuilder builder = new StringBuilder();

            foreach (ComponentTypeItem item in componentHistory) {
                if (item == null || item.Type == null) {
                    continue;
                }

                builder.AppendLine(item.Type.AssemblyQualifiedName);
            }

            EditorPrefs.SetString(HistoryPrefsKey, builder.ToString());
        }

        // Component型のUnity標準アイコンを取得する処理
        private static Texture GetComponentIcon(Type type) {
            if (type == null) {
                return null;
            }

            GUIContent content = EditorGUIUtility.ObjectContent(null, type);

            if (content != null && content.image != null) {
                return content.image;
            }

            return EditorGUIUtility.IconContent(IconNames.FallbackScript).image;
        }

        // Unity Editor標準アイコンを候補順に取得する処理
        private static GUIContent GetEditorIconContent(string[] iconNames, string fallbackText, string tooltip) {
            foreach (string iconName in iconNames) {
                if (string.IsNullOrEmpty(iconName)) {
                    continue;
                }

                GUIContent content = EditorGUIUtility.IconContent(iconName);

                if (content != null && content.image != null) {
                    return new GUIContent(content.image, tooltip);
                }
            }

            return new GUIContent(fallbackText, tooltip);
        }

        // Component型を複数検索選択するドロップダウンWindow
        private class ComponentTypeDropdown : EditorWindow {
            // 検索対象のGameObject
            private GameObject targetObject;

            // Component選択確定時に呼び出す処理
            private Action<List<Type>> onApply;

            // 検索入力文字列
            private string searchText = string.Empty;

            // Component一覧のスクロール位置
            private Vector2 scroll;

            // Component型一覧
            private readonly List<ComponentTypeItem> items = new List<ComponentTypeItem>();

            // 選択中Component型一覧
            private readonly HashSet<Type> selectedTypes = new HashSet<Type>();

            // ドロップダウンを表示する処理
            public static void Show(Rect buttonRect, List<Type> currentTypes, GameObject targetObject, Action<List<Type>> onApply) {
                ComponentTypeDropdown window = CreateInstance<ComponentTypeDropdown>();
                window.targetObject = targetObject;
                window.onApply = onApply;
                window.titleContent = new GUIContent(Text.ComponentDropdownTitle);

                if (currentTypes != null) {
                    foreach (Type type in currentTypes) {
                        if (type != null) {
                            window.selectedTypes.Add(type);
                        }
                    }
                }

                window.BuildItems();

                Rect screenRect = GUIUtility.GUIToScreenRect(buttonRect);
                window.ShowAsDropDown(screenRect, new Vector2(Layout.DropdownWindowWidth, Layout.DropdownWindowHeight));
            }

            // ドロップダウン描画処理
            private void OnGUI() {
                DrawToolbar();
                DrawSearchField();
                DrawComponentList();
                DrawFooter();
            }

            // 上部操作欄を描画する処理
            private void DrawToolbar() {
                EditorGUILayout.Space(Layout.WindowSpaceSmall);

                using (new EditorGUILayout.HorizontalScope()) {
                    if (GUILayout.Button(Text.AllButton, GUILayout.Width(Layout.SmallButtonWidth))) {
                        SelectAllItems();
                    }

                    if (GUILayout.Button(Text.NoneButton, GUILayout.Width(Layout.SmallButtonWidth))) {
                        selectedTypes.Clear();
                    }

                    GUILayout.FlexibleSpace();
                }
            }

            // 表示中Component型を全て選択する処理
            private void SelectAllItems() {
                foreach (ComponentTypeItem item in items) {
                    if (item != null && item.Type != null) {
                        selectedTypes.Add(item.Type);
                    }
                }
            }

            // 検索入力欄を描画する処理
            private void DrawSearchField() {
                EditorGUILayout.Space(Layout.WindowSpaceSmall);
                GUI.SetNextControlName(Text.ComponentSearchControlName);

                EditorGUI.BeginChangeCheck();
                searchText = EditorGUILayout.TextField(searchText, EditorStyles.toolbarSearchField);

                if (EditorGUI.EndChangeCheck()) {
                    scroll = Vector2.zero;
                }

                if (Event.current.type == EventType.Repaint) {
                    EditorGUI.FocusTextInControl(Text.ComponentSearchControlName);
                }
            }

            // Component型一覧を描画する処理
            private void DrawComponentList() {
                EditorGUILayout.Space(Layout.WindowSpaceSmall);
                scroll = EditorGUILayout.BeginScrollView(scroll);

                string normalizedSearch = Normalize(searchText);

                foreach (ComponentTypeItem item in items) {
                    if (item == null || item.Type == null || !MatchesSearch(item, normalizedSearch)) {
                        continue;
                    }

                    DrawComponentRow(item);
                }

                EditorGUILayout.EndScrollView();
            }

            // Component型が検索文字列に一致するか判定する処理
            private static bool MatchesSearch(ComponentTypeItem item, string normalizedSearch) {
                if (string.IsNullOrEmpty(normalizedSearch)) {
                    return true;
                }

                string display = Normalize(item.DisplayName);
                string fullName = Normalize(item.FullName);

                return display.Contains(normalizedSearch) || fullName.Contains(normalizedSearch);
            }

            // 下部操作欄を描画する処理
            private void DrawFooter() {
                EditorGUILayout.Space(Layout.WindowSpaceSmall);

                using (new EditorGUILayout.HorizontalScope()) {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(Text.ApplyButton, GUILayout.Width(Layout.FooterButtonWidth), GUILayout.Height(Layout.FooterButtonHeight))) {
                        ApplyAndClose();
                    }

                    if (GUILayout.Button(Text.CancelButton, GUILayout.Width(Layout.FooterButtonWidth), GUILayout.Height(Layout.FooterButtonHeight))) {
                        Close();
                    }
                }

                EditorGUILayout.Space(Layout.WindowSpaceSmall);
            }

            // Component型の1行表示を描画する処理
            private void DrawComponentRow(ComponentTypeItem item) {
                Rect rowRect = EditorGUILayout.GetControlRect(false, Layout.DropdownRowHeight);
                bool isSelected = selectedTypes.Contains(item.Type);

                if (isSelected && Event.current.type == EventType.Repaint) {
                    EditorStyles.selectionRect.Draw(rowRect, false, true, true, true);
                }

                Rect toggleRect = new Rect(rowRect.x + Layout.DropdownRowToggleX, rowRect.y + Layout.DropdownRowToggleY, Layout.DropdownToggleSize, Layout.DropdownToggleSize);
                Rect iconRect = new Rect(rowRect.x + Layout.DropdownRowIconX, rowRect.y + Layout.DropdownRowIconY, Layout.DropdownIconSize, Layout.DropdownIconSize);
                Rect nameRect = new Rect(rowRect.x + Layout.DropdownNameOffset, rowRect.y + Layout.DropdownRowToggleY, Layout.DropdownFullNameOffset - Layout.DropdownNameOffset - Layout.DropdownNamePadding, Layout.DropdownRowHeight);
                Rect fullNameRect = new Rect(rowRect.x + Layout.DropdownFullNameOffset, rowRect.y + Layout.DropdownRowToggleY, rowRect.width - Layout.DropdownFullNameOffset - Layout.DropdownRowRightPadding, Layout.DropdownRowHeight);

                EditorGUI.BeginChangeCheck();
                bool newSelected = EditorGUI.Toggle(toggleRect, isSelected);

                if (EditorGUI.EndChangeCheck()) {
                    SetSelected(item.Type, newSelected);
                }

                if (item.Icon != null) {
                    GUI.DrawTexture(iconRect, item.Icon, ScaleMode.ScaleToFit);
                }

                EditorGUI.LabelField(nameRect, item.DisplayName, EditorStyles.label);
                EditorGUI.LabelField(fullNameRect, item.FullName, EditorStyles.miniLabel);

                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition)) {
                    SetSelected(item.Type, !isSelected);
                    Event.current.Use();
                }
            }

            // Component型の選択状態を変更する処理
            private void SetSelected(Type type, bool selected) {
                if (type == null) {
                    return;
                }

                if (selected) {
                    selectedTypes.Add(type);
                    return;
                }

                selectedTypes.Remove(type);
            }

            // 選択状態を親Windowへ反映して閉じる処理
            private void ApplyAndClose() {
                List<Type> types = selectedTypes
                    .Where(type => type != null)
                    .OrderBy(type => ObjectNames.NicifyVariableName(type.Name), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(type => type.FullName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                onApply?.Invoke(types);
                Close();
            }

            // Hierarchy内で使用中のComponent型一覧を構築する処理
            private void BuildItems() {
                items.Clear();

                HashSet<Type> types = new HashSet<Type>();

                foreach (GameObject obj in GetSearchTargetObjects(targetObject)) {
                    if (obj == null) {
                        continue;
                    }

                    Component[] components = obj.GetComponents<Component>();

                    foreach (Component component in components) {
                        if (component == null) {
                            continue;
                        }

                        Type type = component.GetType();

                        if (IsValidComponentType(type)) {
                            types.Add(type);
                        }
                    }
                }

                foreach (Type type in types
                    .OrderBy(type => ObjectNames.NicifyVariableName(type.Name), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(type => type.FullName, StringComparer.OrdinalIgnoreCase)) {
                    items.Add(new ComponentTypeItem(type));
                }
            }

            // 検索候補に表示できるComponent型かどうかを判定する処理
            private static bool IsValidComponentType(Type type) {
                if (type == null) {
                    return false;
                }

                if (!typeof(Component).IsAssignableFrom(type)) {
                    return false;
                }

                if (type.IsAbstract) {
                    return false;
                }

                if (type.IsGenericType) {
                    return false;
                }

                if (type.IsGenericTypeDefinition) {
                    return false;
                }

                return true;
            }

            // 検索比較用に文字列を正規化する処理
            private static string Normalize(string value) {
                if (string.IsNullOrEmpty(value)) {
                    return string.Empty;
                }

                return value.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
            }
        }

        // Component型の表示情報を保持するデータ
        private class ComponentTypeItem {
            // Componentの型情報
            public readonly Type Type;

            // Componentの表示名
            public readonly string DisplayName;

            // Componentの完全修飾名
            public readonly string FullName;

            // Componentのアイコン
            public readonly Texture Icon;

            // Component型情報を初期化する処理
            public ComponentTypeItem(Type type) {
                Type = type;
                DisplayName = type == null ? string.Empty : ObjectNames.NicifyVariableName(type.Name);
                FullName = type == null ? string.Empty : type.FullName;
                Icon = GetComponentIcon(type);
            }
        }
    }
}
#endif