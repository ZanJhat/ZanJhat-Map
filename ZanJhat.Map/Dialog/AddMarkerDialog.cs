using System.Xml.Linq;
using Engine;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Game;
using ZanJhat.Core;

namespace ZanJhat.Map
{
    public class AddMarkerDialog : Dialog
    {
        public Vector3? CrossWorldPosition = null;

        private ComponentMinimap m_componentMinimap;

        private SubsystemWorldMap m_subsystemWorldMap => m_componentMinimap?.m_subsystemWorldMap;

        private SubsystemMapMarkers m_subsystemMapMarkers => m_componentMinimap?.m_subsystemMapMarkers;

        public TextBoxWidget m_nameTextBox;
        public ButtonWidget m_colorButton;
        public TextBoxWidget m_xTextBox;
        public TextBoxWidget m_yTextBox;
        public TextBoxWidget m_zTextBox;
        public CheckboxWidget m_clampToEdgeCheckbox;
        public LabelWidget m_stateLabel;
        public ButtonWidget m_addButton;
        public ButtonWidget m_cancelButton;

        public Color[] m_colors = new Color[]
        {
            new Color(255, 80, 80), // 🔴 đỏ
            new Color(80, 200, 255), // 🔵 xanh dương sáng
            new Color(80, 255, 120), // 🟢 xanh lá sáng
            new Color(255, 220, 80), // 🟡 vàng
            new Color(255, 120, 255), // 🟣 hồng tím
            new Color(80, 255, 255), // 🔷 cyan
            new Color(255, 160, 80), // 🟠 cam
            new Color(220, 220, 220) // ⚪ trắng
        };

        public AddMarkerDialog(ComponentMinimap componentMinimap, Vector3? crossWorldPosition = null)
        {
            XElement node = ContentManager.Get<XElement>("Dialogs/AddMarkerDialog");
            LoadContents(this, node);

            m_componentMinimap = componentMinimap;
            CrossWorldPosition = crossWorldPosition;

            m_nameTextBox = Children.Find<TextBoxWidget>("NameTextBox");
            m_colorButton = Children.Find<ButtonWidget>("ColorButton");
            m_xTextBox = Children.Find<TextBoxWidget>("XTextBox");
            m_yTextBox = Children.Find<TextBoxWidget>("YTextBox");
            m_zTextBox = Children.Find<TextBoxWidget>("ZTextBox");
            m_clampToEdgeCheckbox = Children.Find<CheckboxWidget>("ClampToEdgeCheckbox");
            m_stateLabel = Children.Find<LabelWidget>("StateLabel");
            m_addButton = Children.Find<ButtonWidget>("AddButton");
            m_cancelButton = Children.Find<ButtonWidget>("CancelButton");

            m_colorButton.Color = m_colors[0];

            if (CrossWorldPosition.HasValue)
            {
                Vector3 pos = CrossWorldPosition.Value;

                int x = Terrain.ToCell(pos.X);
                int y = Terrain.ToCell(pos.Y);
                int z = Terrain.ToCell(pos.Z);

                m_xTextBox.Text = x.ToString();
                m_yTextBox.Text = y.ToString();
                m_zTextBox.Text = z.ToString();
            }
        }

        public override void Update()
        {
            if (Input.Cancel || m_cancelButton.IsClicked)
            {
                Dismiss();
            }

            if (m_colorButton.IsClicked)
            {
                m_colorButton.Color = m_colors[(m_colors.FirstIndex(m_colorButton.Color) + 1) % m_colors.Length];
            }

            if (m_clampToEdgeCheckbox.IsClicked)
            {
                m_clampToEdgeCheckbox.IsChecked = !m_clampToEdgeCheckbox.IsChecked;
            }

            string name = m_nameTextBox.Text.Trim();

            int x, y, z;

            bool clampToEdge = m_clampToEdgeCheckbox.IsChecked;

            // Name empty
            if (string.IsNullOrWhiteSpace(name))
            {
                m_stateLabel.Text = "Please enter a marker name";
                m_stateLabel.Color = Color.Red;
                return;
            }

            // Name exists
            if (m_subsystemMapMarkers.FindMarker(name) != null)
            {
                m_stateLabel.Text = "Marker name already exists";
                m_stateLabel.Color = Color.Red;
                return;
            }

            // Coordinates
            if (!int.TryParse(m_xTextBox.Text, out x) ||
                !int.TryParse(m_yTextBox.Text, out y) ||
                !int.TryParse(m_zTextBox.Text, out z))
            {
                m_stateLabel.Text = "Coordinates must be integers";
                m_stateLabel.Color = Color.Red;
                return;
            }

            // Max markers
            if (m_subsystemMapMarkers.Markers.Count(m => m.MarkerType == MarkerType.Default) >= 20)
            {
                m_stateLabel.Text = "Maximum 20 markers allowed";
                m_stateLabel.Color = Color.Red;
                return;
            }

            // Valid
            m_stateLabel.Text = "Ready to add marker";
            m_stateLabel.Color = Color.LightGreen;

            if (m_addButton.IsClicked)
            {
                Color color = m_colorButton.Color;

                m_subsystemMapMarkers.AddMarker(
                    name, x, y, z, color, clampToEdge, false,
                    m_componentMinimap.m_componentPlayer
                );

                Dismiss();
            }
        }

        public void Dismiss()
        {
            DialogsManager.HideDialog(this);
        }
    }
}
