using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Conecting
{
    public class SessionTabItem
    {
        public string Title { get; set; }
        public RemoteSessionView SessionView { get; set; }
        public ModernButton TabButton { get; set; }
        public Button CloseButton { get; set; }
    }

    /// <summary>
    /// AnyDesk-Style Unified Multi-Session Navigation & Tab System.
    /// Integrates remote session tabs directly into the main top navigation bar.
    /// </summary>
    public class SessionTabControl : Panel
    {
        private FlowLayoutPanel navTabsFlowPanel;
        private Panel contentContainerPanel;
        private ModernButton btnDashboardTab;
        private ModernButton btnNewTab;
        private List<SessionTabItem> activeTabs = new List<SessionTabItem>();
        private Panel dashboardContentPanel;
        private Panel settingsContentPanel;
        private Panel topHeaderPanel;
        private SessionTabItem currentSelectedTab = null;
        private bool isSettingsViewActive = false;

        public Action OnNewTabClick { get; set; }

        public SessionTabControl(FlowLayoutPanel navFlow, Panel contentContainer, Panel dashboardPanel, Panel settingsPanel, Panel topHeader)
        {
            this.navTabsFlowPanel = navFlow;
            this.contentContainerPanel = contentContainer;
            this.dashboardContentPanel = dashboardPanel;
            this.settingsContentPanel = settingsPanel;
            this.topHeaderPanel = topHeader;

            InitializeTabs();
        }

        private void InitializeTabs()
        {
            navTabsFlowPanel.Controls.Clear();

            btnDashboardTab = new ModernButton
            {
                Text = AppI18n.T("Puesto de Trabajo", "Workstation"),
                Size = new Size(160, 34),
                NormalColor = Color.FromArgb(14, 98, 115),
                HoverColor = Color.FromArgb(8, 70, 84),
                ForeColor = Color.White,
                BorderRadius = 6,
                Margin = new Padding(0, 2, 6, 0)
            };
            btnDashboardTab.Click += (s, e) => { SelectDashboardTab(); };

            btnNewTab = new ModernButton
            {
                Text = "  +  ",
                Size = new Size(38, 34),
                NormalColor = Color.FromArgb(241, 245, 249),
                HoverColor = Color.FromArgb(226, 232, 240),
                ForeColor = Color.FromArgb(15, 23, 42),
                BorderRadius = 6,
                Margin = new Padding(4, 2, 6, 0),
                Visible = false
            };
            btnNewTab.Click += (s, e) =>
            {
                SelectDashboardTab();
                if (OnNewTabClick != null) OnNewTabClick();
            };

            navTabsFlowPanel.Controls.Add(btnDashboardTab);
            navTabsFlowPanel.Controls.Add(btnNewTab);

            // Add dashboard & settings panels to container
            dashboardContentPanel.Dock = DockStyle.Fill;
            if (!contentContainerPanel.Controls.Contains(dashboardContentPanel))
            {
                contentContainerPanel.Controls.Add(dashboardContentPanel);
            }

            settingsContentPanel.Dock = DockStyle.Fill;
            if (!contentContainerPanel.Controls.Contains(settingsContentPanel))
            {
                contentContainerPanel.Controls.Add(settingsContentPanel);
            }
            settingsContentPanel.Visible = false;

            dashboardContentPanel.Visible = true;
            dashboardContentPanel.BringToFront();
        }

        public void SelectDashboardTab()
        {
            currentSelectedTab = null;
            isSettingsViewActive = false;

            topHeaderPanel.Visible = true;
            settingsContentPanel.Visible = false;

            HideAllSessionActionsPanels();

            foreach (Control c in contentContainerPanel.Controls)
            {
                if (c != dashboardContentPanel) c.Visible = false;
            }

            dashboardContentPanel.Visible = true;
            dashboardContentPanel.BringToFront();
            UpdateHeaderStyles();
        }

        public void SelectSettingsTab()
        {
            currentSelectedTab = null;
            isSettingsViewActive = true;

            topHeaderPanel.Visible = true;
            dashboardContentPanel.Visible = false;

            HideAllSessionActionsPanels();

            foreach (Control c in contentContainerPanel.Controls)
            {
                if (c != settingsContentPanel) c.Visible = false;
            }

            settingsContentPanel.Visible = true;
            settingsContentPanel.BringToFront();
            UpdateHeaderStyles();
        }

        private void HideAllSessionActionsPanels()
        {
            try
            {
                Control navHeader = navTabsFlowPanel.Parent;
                if (navHeader != null)
                {
                    foreach (var item in activeTabs)
                    {
                        if (item.SessionView != null && item.SessionView.SessionActionsPanel != null)
                        {
                            if (navHeader.Controls.Contains(item.SessionView.SessionActionsPanel))
                            {
                                navHeader.Controls.Remove(item.SessionView.SessionActionsPanel);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        public void AddSessionTab(RemoteSessionView sessionView)
        {
            string tabTitle = string.Format("{0} ({1})", sessionView.Hostname, sessionView.TargetId);

            Panel tabCard = new Panel
            {
                Size = new Size(200, 36),
                BackColor = Color.FromArgb(241, 245, 249),
                Margin = new Padding(0, 2, 6, 0)
            };

            ModernButton btnTab = new ModernButton
            {
                Text = tabTitle,
                Size = new Size(168, 34),
                Location = new Point(0, 0),
                NormalColor = Color.FromArgb(241, 245, 249),
                HoverColor = Color.FromArgb(226, 232, 240),
                ForeColor = Color.FromArgb(15, 23, 42),
                BorderRadius = 6,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };

            Button btnClose = new Button
            {
                Text = "✕",
                Size = new Size(22, 22),
                Location = new Point(172, 6),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            btnClose.FlatAppearance.BorderSize = 0;

            SessionTabItem tabItem = new SessionTabItem
            {
                Title = tabTitle,
                SessionView = sessionView,
                TabButton = btnTab,
                CloseButton = btnClose
            };

            btnTab.Click += (s, e) => { SelectSessionTab(tabItem); };
            btnClose.Click += (s, e) => { CloseSessionTab(tabItem); };

            sessionView.OnCloseSessionRequested = () =>
            {
                this.BeginInvoke((MethodInvoker)delegate { CloseSessionTab(tabItem); });
            };

            tabCard.Controls.Add(btnTab);
            tabCard.Controls.Add(btnClose);

            activeTabs.Add(tabItem);

            // Re-order tab flow: Dashboard -> Active Session Tabs -> [+] New Tab Button
            navTabsFlowPanel.Controls.Remove(btnNewTab);
            navTabsFlowPanel.Controls.Add(tabCard);
            navTabsFlowPanel.Controls.Add(btnNewTab);
            btnNewTab.Visible = true;

            // Add RemoteSessionView control to container
            sessionView.Dock = DockStyle.Fill;
            contentContainerPanel.Controls.Add(sessionView);

            SelectSessionTab(tabItem);
        }

        public void SelectSessionTab(SessionTabItem tabItem)
        {
            if (tabItem == null || !activeTabs.Contains(tabItem)) return;

            currentSelectedTab = tabItem;
            isSettingsViewActive = false;

            // Automatically maximize window to present full remote desktop experience
            try
            {
                Form parentForm = this.FindForm();
                if (parentForm != null)
                {
                    parentForm.WindowState = FormWindowState.Maximized;
                }
            }
            catch { }

            // Hide top header banner to give maximum vertical screen space to remote desktop!
            topHeaderPanel.Visible = false;
            dashboardContentPanel.Visible = false;
            settingsContentPanel.Visible = false;

            HideAllSessionActionsPanels();

            Control navHeader = navTabsFlowPanel.Parent;
            if (navHeader != null && tabItem.SessionView != null && tabItem.SessionView.SessionActionsPanel != null)
            {
                tabItem.SessionView.SessionActionsPanel.Dock = DockStyle.Right;
                navHeader.Controls.Add(tabItem.SessionView.SessionActionsPanel);
                tabItem.SessionView.SessionActionsPanel.BringToFront();
                tabItem.SessionView.SessionActionsPanel.Visible = true;
            }

            foreach (var item in activeTabs)
            {
                if (item == tabItem)
                {
                    item.SessionView.Visible = true;
                    item.SessionView.BringToFront();
                    item.SessionView.Focus();
                }
                else
                {
                    item.SessionView.Visible = false;
                }
            }

            UpdateHeaderStyles();
        }

        public void CloseSessionTab(SessionTabItem tabItem)
        {
            if (tabItem == null || !activeTabs.Contains(tabItem)) return;

            if (tabItem.SessionView != null)
            {
                Control navHeader = navTabsFlowPanel.Parent;
                if (navHeader != null && tabItem.SessionView.SessionActionsPanel != null)
                {
                    if (navHeader.Controls.Contains(tabItem.SessionView.SessionActionsPanel))
                    {
                        navHeader.Controls.Remove(tabItem.SessionView.SessionActionsPanel);
                    }
                }
                tabItem.SessionView.CloseSession();
            }

            activeTabs.Remove(tabItem);

            Control parentCard = tabItem.TabButton.Parent;
            if (parentCard != null)
            {
                navTabsFlowPanel.Controls.Remove(parentCard);
                parentCard.Dispose();
            }

            contentContainerPanel.Controls.Remove(tabItem.SessionView);
            tabItem.SessionView.Dispose();

            if (activeTabs.Count == 0)
            {
                btnNewTab.Visible = false;
                SelectDashboardTab();
            }
            else if (currentSelectedTab == tabItem)
            {
                SelectSessionTab(activeTabs[activeTabs.Count - 1]);
            }
            else
            {
                UpdateHeaderStyles();
            }
        }

        public void UpdateHeaderStyles()
        {
            bool isDashSelected = (currentSelectedTab == null && !isSettingsViewActive);
            btnDashboardTab.NormalColor = isDashSelected ? Color.FromArgb(14, 98, 115) : Color.FromArgb(241, 245, 249);
            btnDashboardTab.HoverColor = isDashSelected ? Color.FromArgb(8, 70, 84) : Color.FromArgb(226, 232, 240);
            btnDashboardTab.ForeColor = isDashSelected ? Color.White : Color.FromArgb(15, 23, 42);
            btnDashboardTab.Invalidate();

            foreach (var item in activeTabs)
            {
                bool isSelected = (item == currentSelectedTab);
                item.TabButton.NormalColor = isSelected ? Color.FromArgb(14, 98, 115) : Color.FromArgb(241, 245, 249);
                item.TabButton.HoverColor = isSelected ? Color.FromArgb(8, 70, 84) : Color.FromArgb(226, 232, 240);
                item.TabButton.ForeColor = isSelected ? Color.White : Color.FromArgb(15, 23, 42);
                item.TabButton.Invalidate();
            }
        }
    }
}
