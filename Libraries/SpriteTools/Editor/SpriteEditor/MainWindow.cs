using Editor;
using Editor.NodeEditor;
using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SpriteTools.SpriteEditor;

[EditorForAssetType("sprite")]
[EditorApp("Sprite Editor", "emoji_emotions", "Edit 2D Sprites")]
public partial class MainWindow : DockWindow, IAssetEditor
{
    public Action OnAssetLoaded;
    public Action OnTextureUpdate;
    public Action OnAnimationChanges;
    public Action OnAnimationSelected;

    internal static List<MainWindow> AllWindows { get; } = new List<MainWindow>();
    public bool CanOpenMultipleAssets => true;

    bool _dirty = true;

    private Asset _asset;
    public SpriteResource Sprite;
    public SpriteAnimation SelectedAnimation
    {
        get => _selectedAnimation;
        set
        {
            _selectedAnimation = value;
            CurrentFrameIndex = 0;
        }
    }
    private SpriteAnimation _selectedAnimation;
    public string CurrentTexturePath => (SelectedAnimation?.Frames?.Count > 0) ? (SelectedAnimation?.Frames[CurrentFrameIndex] ?? "") : "";
    public int CurrentFrameIndex
    {
        get => _currentFrameIndex;
        set
        {
            _currentFrameIndex = value;
            OnTextureUpdate?.Invoke();
        }
    }
    private int _currentFrameIndex = 0;
    RealTimeSince frameTimer = 0;
    float FrameTime => ((SelectedAnimation?.FrameRate ?? 0) == 0) ? 0 : (1f / (SelectedAnimation?.FrameRate ?? 30));

    public bool Playing = true;

    public MainWindow()
    {
        DeleteOnClose = true;

        Size = new Vector2(1280, 720);
        Sprite = new SpriteResource();
        Sprite.Animations.Clear();

        SetWindowIcon("emoji_emotions");

        AllWindows.Add(this);

        RestoreDefaultDockLayout();
    }

    protected override void OnClosed()
    {
        base.OnClosed();

        AllWindows.Remove(this);
    }

    protected override void OnFocus(FocusChangeReason reason)
    {
        base.OnFocus(reason);

        // Move this window to the end of the list, so it has priority
        // when opening a new graph

        AllWindows.Remove(this);
        AllWindows.Add(this);
    }

    public void AssetOpen(Asset asset)
    {
        Open("", asset);
        Show();
    }

    public void SelectMember(string memberName)
    {

    }

    void UpdateWindowTitle()
    {
        Title = $"{_asset.Name} - Sprite Editor" ?? "Untitled Sprite - Sprite Editor";
    }

    public void RebuildUI()
    {
        MenuBar.Clear();

        {
            var file = MenuBar.AddMenu("File");
            file.AddOption("New", "common/new.png", () => New(), "CTRL+N").StatusText = "New Sprite";
            file.AddOption("Open", "common/open.png", () => Open(), "Ctrl+O").StatusText = "Open Sprite";
            file.AddOption("Save", "common/save.png", () => Save(), "Ctrl+S").StatusText = "Save Sprite";
            file.AddOption("Save As...", "common/save.png", () => Save(true), "Ctrl+Shift+S").StatusText = "Save Sprite As...";
            file.AddSeparator();
            file.AddOption(new Option("Exit") { Triggered = Close });
        }

        {
            var edit = MenuBar.AddMenu("Edit");
            // _undoMenuOption = edit.AddOption( "Undo", "undo", Undo, "Ctrl+Z" );
            // _redoMenuOption = edit.AddOption( "Redo", "redo", Redo, "Ctrl+Y" );

            // edit.AddSeparator();
            // edit.AddOption( "Cut", "common/cut.png", CutSelection, "Ctrl+X" );
            // edit.AddOption( "Copy", "common/copy.png", CopySelection, "Ctrl+C" );
            // edit.AddOption( "Paste", "common/paste.png", PasteSelection, "Ctrl+V" );
            // edit.AddOption( "Select All", "select_all", SelectAll, "Ctrl+A" );
        }

        {
            var view = MenuBar.AddMenu("View");

            view.AboutToShow += () => OnViewMenu(view);
        }
    }

    private void OnViewMenu(Menu view)
    {
        view.Clear();
        view.AddOption("Restore To Default", "settings_backup_restore", RestoreDefaultDockLayout);
        view.AddSeparator();

        foreach (var dock in DockManager.DockTypes)
        {
            var o = view.AddOption(dock.Title, dock.Icon);
            o.Checkable = true;
            o.Checked = DockManager.IsDockOpen(dock.Title);
            o.Toggled += (b) => DockManager.SetDockState(dock.Title, b);
        }
    }

    protected override void RestoreDefaultDockLayout()
    {
        var inspector = new Inspector(this);
        var preview = new Preview.Preview(this);
        var timeline = new Timeline.Timeline(this);
        var animationList = new AnimationList.AnimationList(this);
        // var errorList = new ErrorList( null, this );

        DockManager.Clear();
        DockManager.RegisterDockType("Inspector", "edit", () => new Inspector(this));
        DockManager.RegisterDockType("Animations", "directions_walk", () => new AnimationList.AnimationList(this));
        DockManager.RegisterDockType("Preview", "emoji_emotions", () => new Preview.Preview(this));
        DockManager.RegisterDockType("Frames", "view_column", () => new Timeline.Timeline(this));
        // DockManager.RegisterDockType( "ErrorList", "error", () => new ErrorList( null, this ) );

        DockManager.AddDock(null, inspector, DockArea.Left, DockManager.DockProperty.HideOnClose);
        DockManager.AddDock(null, preview, DockArea.Right, DockManager.DockProperty.HideOnClose, split: 0.65f);

        DockManager.AddDock(preview, timeline, DockArea.Bottom, DockManager.DockProperty.HideOnClose, split: 0.2f);
        DockManager.AddDock(inspector, animationList, DockArea.Bottom, DockManager.DockProperty.HideOnClose, split: 0.45f);

        // DockManager.AddDock( inspector, errorList, DockArea.Bottom, DockManager.DockProperty.HideOnClose, split: 0.75f );

        DockManager.Update();

        RebuildUI();
    }

    public void New()
    {
        PromptSave(() => CreateNew());
    }

    public void CreateNew()
    {
        var savePath = GetSavePath("New Sprite");

        _asset = null;
        Sprite = AssetSystem.CreateResource("sprite", savePath).LoadResource<SpriteResource>();
        _dirty = false;

        if (Sprite.Animations.Count > 0)
        {
            SelectedAnimation = Sprite.Animations[0];
            OnAnimationSelected?.Invoke();
        }

        UpdateWindowTitle();
        OnAssetLoaded?.Invoke();
        OnTextureUpdate?.Invoke();
    }

    public void Open()
    {
        var fd = new FileDialog(null)
        {
            Title = "Open Sprite",
            DefaultSuffix = ".sprite"
        };

        fd.SetNameFilter("2D Sprite (*.sprite)");

        if (!fd.Execute()) return;

        PromptSave(() => Open(fd.SelectedFile));
    }

    public void Open(string path, Asset asset = null)
    {
        if (!string.IsNullOrEmpty(path))
        {
            Log.Info($"Opening sprite at path {path}");
            asset ??= AssetSystem.FindByPath(path);
            Log.Info(asset);
        }
        if (asset == null) return;

        if (asset == _asset)
        {
            Log.Warning($"{asset.RelativePath} is already open.");
            return;
        }

        var sprite = asset.LoadResource<SpriteResource>();
        if (sprite == null)
        {
            Log.Warning($"Failed to load sprite from {asset.RelativePath}");
            return;
        }

        StateCookie = asset.RelativePath;

        _asset = asset;
        Sprite = sprite;
        UpdateWindowTitle();

        OnAssetLoaded?.Invoke();
        OnTextureUpdate?.Invoke();

        if ((Sprite.Animations?.Count ?? 0) > 0)
        {
            SelectedAnimation = Sprite.Animations[0];
            OnAnimationSelected?.Invoke();
        }
    }

    public bool Save(bool saveAs = false)
    {
        var savePath = (_asset == null || saveAs) ? GetSavePath() : _asset.AbsolutePath;
        if (string.IsNullOrWhiteSpace(savePath)) return false;

        // Write serialized data to file
        //Log.Info(JsonSerializer.Serialize(Sprite, new JsonSerializerOptions { WriteIndented = true }));
        // System.IO.File.WriteAllText(savePath, _json);
        // Log.Info(_json);

        if (saveAs)
        {
            // If we're saving as, we want to register the new asset
            _asset = null;
        }

        // Register the asset if we haven't already
        _asset ??= AssetSystem.RegisterFile(savePath);
        _asset.SaveToDisk(Sprite);

        if (_asset == null)
        {
            Log.Warning($"Failed to register asset at path {savePath}");
            return false;
        }

        MainAssetBrowser.Instance?.UpdateAssetList();

        return true;
    }

    [EditorEvent.Frame]
    void Frame()
    {
        if (SelectedAnimation is null) return;
        if (SelectedAnimation?.Frames?.Count == 0) return;
        if (FrameTime == 0) return;

        if (Playing)
        {
            while (frameTimer >= FrameTime)
            {
                CurrentFrameIndex = (CurrentFrameIndex + 1) % SelectedAnimation.Frames.Count;
                frameTimer -= FrameTime;
                if (CurrentFrameIndex == 0 && !SelectedAnimation.Looping)
                {
                    Playing = false;
                    CurrentFrameIndex = SelectedAnimation.Frames.Count - 1;
                    frameTimer = 0;
                }
            }
        }
        else
        {
            frameTimer = 0f;
        }
    }

    static string GetSavePath(string title = "Save Sprite")
    {
        var fd = new FileDialog(null)
        {
            Title = title,
            DefaultSuffix = $".sprite"
        };

        fd.SelectFile("untitled.sprite");
        fd.SetFindFile();
        fd.SetModeSave();
        fd.SetNameFilter("2D Sprite (*.sprite)");
        if (!fd.Execute()) return null;

        return fd.SelectedFile;
    }

    void PromptSave(Action action)
    {
        if (!_dirty)
        {
            action?.Invoke();
            return;
        }

        var confirm = new PopupWindow(
            "Save Current Sprite", "The open sprite has unsaved changes. Would you like to save before continuing?", "Cancel",
            new Dictionary<string, Action>
            {
                { "No", () => {
                    action?.Invoke();
                } },
                { "Yes", () => {
                    if (Save()) action?.Invoke();
                }}
            });
        confirm.Show();
    }

    public void PlayPause()
    {
        Playing = !Playing;
        if (Playing && !SelectedAnimation.Looping && CurrentFrameIndex >= SelectedAnimation.Frames.Count - 1)
        {
            CurrentFrameIndex = 0;
        }
    }
}
