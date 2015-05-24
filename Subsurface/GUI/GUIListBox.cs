﻿using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Subsurface
{
    class GUIListBox : GUIComponent
    {
        protected GUIComponent selected;

        public delegate bool OnSelectedHandler(object obj);
        public OnSelectedHandler OnSelected;

        public delegate object CheckSelectedHandler();
        public CheckSelectedHandler CheckSelected;

        private GUIScrollBar scrollBar;

        private int totalSize;

        private int spacing;

        private bool scrollBarEnabled;
        private bool scrollBarHidden;

        private bool enabled;

        public object SelectedData
        {
            get { return (selected == null) ? null : selected.UserData; }
        }

        public int SelectedIndex
        {
            get
            {
                if (selected == null) return -1;
                return children.FindIndex(x => x == selected);
            }
        }

        public int Spacing
        {
            get { return spacing; }
            set { spacing = value; }
        }

        public bool Enabled
        {
            get { return enabled; }
            set { enabled = value; }
        }

        public bool ScrollBarEnabled
        {
            get { return scrollBarEnabled; }
            set 
            { 
                if (value)
                {
                    if (!scrollBarEnabled && scrollBarHidden) ShowScrollBar();                    
                }
                else
                {
                    if (scrollBarEnabled && !scrollBarHidden) HideScrollBar();                    
                }

                scrollBarEnabled = value; 
            }
        }

        public GUIListBox(Rectangle rect, GUIStyle style, Alignment alignment, GUIComponent parent = null)
            : this(rect, style.foreGroundColor, alignment, parent)
        {
        }

        public GUIListBox(Rectangle rect, Color color, GUIComponent parent = null)
            : this(rect, color, (Alignment.Left | Alignment.Top), parent)
        {            
        }

        public GUIListBox(Rectangle rect, Color color, Alignment alignment, GUIComponent parent = null)
        {
            this.rect = rect;
            this.alignment = alignment;

            this.color = color;

            if (parent != null)
                parent.AddChild(this);

            this.rect.Width -= 20;

            scrollBar = new GUIScrollBar(
                new Rectangle(this.rect.X + this.rect.Width, this.rect.Y, 20, this.rect.Height), color, 1.0f);

            UpdateScrollBarSize();

            enabled = true;

            scrollBarEnabled = true;
        }

        public void Select(object selection)
        {
            foreach (GUIComponent child in children)
            {
                if (child.UserData != selection) continue;
                
                selected = child;
                if (OnSelected != null) OnSelected(selected.UserData);
                return;                
            }
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            scrollBar.Update(deltaTime);
        }

        public void Select(int childIndex)
        {
            if (childIndex >= children.Count || childIndex<0) return;

            selected = children[childIndex];
            if (OnSelected != null) OnSelected(selected.UserData);
        }

        public void Deselect()
        {
            selected = null;
        }

        public void UpdateScrollBarSize()
        {
            totalSize = 0;
            foreach (GUIComponent child in children)
            {
                totalSize += (scrollBar.IsHorizontal) ? child.Rect.Width : child.Rect.Height;
                totalSize += spacing;
            }

            scrollBar.BarSize = scrollBar.IsHorizontal ? 
                Math.Min((float)rect.Width / (float)totalSize, 1.0f) : 
                Math.Min((float)rect.Height / (float)totalSize, 1.0f);

            if (scrollBar.BarSize < 1.0f && scrollBarHidden) ShowScrollBar();
            if (scrollBar.BarSize >= 1.0f && !scrollBarHidden) HideScrollBar();
        }

        public override void AddChild(GUIComponent child)
        {
            base.AddChild(child);

            float oldScroll = scrollBar.BarScroll;
            float oldSize = scrollBar.BarSize;
            UpdateScrollBarSize();
            
            if (scrollBar.BarSize<1.0f && (oldSize>=1.0f || oldScroll==1.0f))
            {
                scrollBar.BarScroll = 1.0f;
            }
            
        }

        public override void RemoveChild(GUIComponent child)
        {
            base.RemoveChild(child);

            UpdateScrollBarSize();
            
        }

        private void ShowScrollBar()
        {
            scrollBarHidden = false;
            Rect = new Rectangle(rect.X, rect.Y, rect.Width - scrollBar.Rect.Width, rect.Height);
        }

        private void HideScrollBar()
        {
            scrollBarHidden = true;
            Rect = new Rectangle(rect.X, rect.Y, rect.Width + scrollBar.Rect.Width, rect.Height);
        }

        public override void Draw(SpriteBatch spriteBatch)
        {

            GUI.DrawRectangle(spriteBatch, rect, color*alpha, true);

            int x = rect.X, y = rect.Y;

            if (!scrollBarHidden)
            {
                scrollBar.Draw(spriteBatch);
                if (scrollBar.IsHorizontal)
                {
                    x -= (int)((totalSize - rect.Height) * scrollBar.BarScroll);
                }
                else
                {
                    y -= (int)((totalSize - rect.Height) * scrollBar.BarScroll);
                }
            }


            for (int i = 0; i < children.Count; i++ )
            {
                GUIComponent child = children[i];

                child.Rect = new Rectangle(child.Rect.X, y, child.Rect.Width, child.Rect.Height);
                y += child.Rect.Height + spacing;

                if (child.Rect.Y + child.Rect.Height < rect.Y) continue;
                if (child.Rect.Y + child.Rect.Height > rect.Y + rect.Height) break;

                if (child.Rect.Y < rect.Y && child.Rect.Y + child.Rect.Height >= rect.Y)
                {
                    y = rect.Y;
                    continue;
                }

                if (selected == child)
                {
                    child.State = ComponentState.Selected;

                    if (CheckSelected != null)
                    {
                        if (CheckSelected() != selected.UserData) selected = null;
                    }
                }
                else if (child.Rect.Contains(PlayerInput.GetMouseState.Position) && enabled)
                {
                    child.State = ComponentState.Hover;
                    if (PlayerInput.LeftButtonClicked())
                    {
                        Debug.WriteLine("clicked");
                        selected = child;
                        if (OnSelected != null)
                            OnSelected(child.UserData);
                    }
                }
                else
                {
                    child.State = ComponentState.None;
                }

                child.Draw(spriteBatch);
            }

            //GUI.DrawRectangle(spriteBatch, rect, Color.Black, false);
        }
    }
}
