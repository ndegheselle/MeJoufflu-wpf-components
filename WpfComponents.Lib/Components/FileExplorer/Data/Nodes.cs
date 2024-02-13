﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using WpfComponents.Lib.Logic.Windows;

namespace WpfComponents.Lib.Components.FileExplorer.Data
{
    public enum EnumExplorerNodeType
    {
        Folder,
        File
    }

    [Flags]
    public enum EnumExplorerNodeState
    {
        MovingInProgress,
    }

    public class ExplorerNodeFile : ExplorerNode
    {
        public ExplorerNodeFile(string path)
        {
            Type = EnumExplorerNodeType.File;
            FullPath = path;
        }
    }

    public class ExplorerNodeFolder : ExplorerNode
    {
        public event Action OnRefresh;

        public ObservableCollection<ExplorerNode> Children { get; set; } = new ObservableCollection<ExplorerNode>();
        public ListCollectionView SortedChildrenView { get; }

        private bool _isOpen;
        public bool IsOpen
        {
            get { return _isOpen; }
            set
            {
                _isOpen = value;
                OnPropertyChanged();
            }
        }

        public ExplorerNodeFolder(string path)
        {
            Type = EnumExplorerNodeType.Folder;
            FullPath = path;
            SortedChildrenView = CollectionViewSource.GetDefaultView(Children) as ListCollectionView;
            SortedChildrenView.CustomSort = new NameComparer(ListSortDirection.Ascending);
        }

        public void Add(ExplorerNode childNode)
        {
            Children.Add(childNode);
            childNode.Parent = this;
        }

        public void Refresh()
        {
            SortedChildrenView.Refresh();
        }

        public void RecursiveForEach(Action<ExplorerNode> callback)
        {
            callback.Invoke(this);
            foreach (var node in Children)
            {
                if (node is ExplorerNodeFolder folderNode)
                {
                    folderNode.RecursiveForEach(callback);
                }
                else if (node is ExplorerNodeFile fileNode)
                {
                    callback.Invoke(fileNode);
                }
            }
        }

        public void UpdateChildren(int depth = 1, bool force = false)
        {
            if (force)
                Children.Clear();

            foreach (string folderPath in Directory.GetDirectories(this.FullPath, "*", SearchOption.TopDirectoryOnly))
            {
                var childFolderNode = new ExplorerNodeFolder(folderPath);
                if (!force && Children.Contains(childFolderNode))
                {
                    childFolderNode = Children.First(x => x.FullPath == folderPath) as ExplorerNodeFolder;
                }
                else
                {
                    Add(childFolderNode);
                }

                if (depth > 0)
                    childFolderNode.UpdateChildren(depth - 1, force);
            }

            foreach (string filePath in Directory.GetFiles(this.FullPath, "*", SearchOption.TopDirectoryOnly))
            {
                var childFileNode = new ExplorerNodeFile(filePath);
                if (force || !Children.Contains(childFileNode))
                    Add(childFileNode);
            }
            Refresh();
        }

        public List<string> RelativePath(string path)
        {
            List<string> pathSegments = path.Split(Path.DirectorySeparatorChar).ToList();
            string[] rootFolders = this.FullPath.Split(Path.DirectorySeparatorChar);

            // Get the folders relative to the root folder
            pathSegments.RemoveRange(0, rootFolders.Length);
            return pathSegments;
        }

        /// <summary>
        /// Search for a node in the tree.
        /// </summary>
        /// <param name="path"></param>
        /// <returns>The node corresponding to the path if found, otherwise null.</returns>
        public ExplorerNode SearchChildNode(string path)
        {
            List<string> pathSegments = RelativePath(path);

            if (pathSegments.Count == 0)
                return this;

            return SearchNode(this.Children, pathSegments, 0);
        }

        private ExplorerNode SearchNode(ObservableCollection<ExplorerNode> children, List<string> pathSegments, int index)
        {
            foreach (var node in children)
            {
                if (node.Name == pathSegments[index])
                {
                    if (index == pathSegments.Count - 1)
                        return node;
                    else if (node as ExplorerNodeFolder != null)
                        return SearchNode((node as ExplorerNodeFolder).Children, pathSegments, index + 1);
                }
            }
            return null;
        }
    }

    public abstract class ExplorerNode : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private bool _isEditing;
        public bool IsEditing
        {
            get { return _isEditing; }
            set
            {
                _isEditing = value;
                OnPropertyChanged();
            }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        // If the node should be the only one selected in the tree (for TreeView)
        private bool _isUniqueSelected;
        public bool IsUniqueSelected
        {
            get { return _isUniqueSelected; }
            set
            {
                _isUniqueSelected = value;
                OnPropertyChanged();
            }
        }

        public string Name
        {
            get { return Path.GetFileName(FullPath); }
        }

        private string _fullPath;
        public string FullPath
        {
            get { return _fullPath; }
            set
            {
                _fullPath = value;
                IsSelected = false;
                if (Type == EnumExplorerNodeType.Folder)
                    Info = new DirectoryInfo(_fullPath);
                else
                    Info = new FileInfo(_fullPath);

                if (Context?.CustomIcon == null)
                    Icon = SystemIcons.GetIcon(_fullPath, true);
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(Info));
                OnPropertyChanged();
            }
        }

        public ExplorerContext Context { get; set; }

        // These props don't need to notify
        public ExplorerNodeFolder Parent { get; set; }
        public ImageSource Icon { get; set; }
        public FileSystemInfo Info { get; set; }

        public EnumExplorerNodeType Type { get; set; }

        // Convenient for debugging
        public override string ToString()
        {
            return $"{Type}:{Name}";
        }

        public static bool operator ==(ExplorerNode? a, ExplorerNode? b)
        {
            if (a is null && b is null) return true;
            if (a is null || b is null) return false;
            return a.FullPath == b.FullPath;
        }
        public static bool operator !=(ExplorerNode a, ExplorerNode b)
        {
            return !(a == b);
        }

        public override bool Equals(object? obj)
        {
            if (obj is ExplorerNode == false) return false;
            return this.FullPath == (obj as ExplorerNode)?.FullPath;
        }

        public override int GetHashCode()
        {
            return FullPath.GetHashCode();
        }

        public ExplorerNodeFolder? SearchRoot(ExplorerNode? node = null)
        {
            if (node == null) node = this;
            if (node.Parent == null) return node as ExplorerNodeFolder;

            return SearchRoot(node.Parent);
        }
    }

    // XXX: if more things are added here, create subclasses
    public class ExplorerContext
    {
        // Display
        public string CustomIcon { get; set; }
        public Brush CustomIconColor { get; set; }

        // Context menu
        public string MenuName { get; set; } = null;
        public dynamic Data { get; set; }
        public ICommand Command { get; set; }
    }
}