using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CatFishGame
{
    /// <summary>
    /// Главное меню игры "Котик и Рыбки".
    /// Содержит заголовок, краткое описание, кнопки "Играть" и "Выход",
    /// а также анимированную иллюстрацию (котик ловит рыбку).
    /// </summary>
    public class MenuForm : Form
    {
        private Button btnPlay;
        private Button btnExit;
        private Timer animTimer;
        private int animTick = 0;

        public MenuForm()
        {
            this.Text = "Котик и Рыбки — Меню";
            this.ClientSize = new Size(640, 480);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(255, 250, 235);
            this.DoubleBuffered = true;
            this.Font = new Font("Segoe UI", 11F);

            BuildControls();
            BuildTimer();
        }

        private void BuildControls()
        {
            // Кнопка "Играть"
            btnPlay = new Button();
            btnPlay.Text = "Играть";
            btnPlay.Size = new Size(220, 50);
            btnPlay.Location = new Point(210, 320);
            btnPlay.BackColor = Color.FromArgb(255, 180, 100);
            btnPlay.FlatStyle = FlatStyle.Flat;
            btnPlay.FlatAppearance.BorderSize = 0;
            btnPlay.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            btnPlay.ForeColor = Color.White;
            btnPlay.Click += BtnPlay_Click;

            // Кнопка "Выход"
            btnExit = new Button();
            btnExit.Text = "Выход";
            btnExit.Size = new Size(220, 40);
            btnExit.Location = new Point(210, 385);
            btnExit.BackColor = Color.FromArgb(150, 150, 160);
            btnExit.FlatStyle = FlatStyle.Flat;
            btnExit.FlatAppearance.BorderSize = 0;
            btnExit.Font = new Font("Segoe UI", 12F);
            btnExit.ForeColor = Color.White;
            btnExit.Click += BtnExit_Click;

            this.Controls.Add(btnPlay);
            this.Controls.Add(btnExit);
        }

        private void BuildTimer()
        {
            animTimer = new Timer();
            animTimer.Interval = 30; // ~33 fps
            animTimer.Tick += (s, e) => { animTick++; this.Invalidate(); };
            animTimer.Start();
        }

        private void BtnPlay_Click(object sender, EventArgs e)
        {
            this.Hide();
            var game = new GameForm();
            game.FormClosed += (s, ea) => { this.Show(); };
            game.Show();
        }

        private void BtnExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Фон — мягкий градиент
            using (var bg = new LinearGradientBrush(
                new Rectangle(0, 0, this.ClientSize.Width, this.ClientSize.Height),
                Color.FromArgb(255, 250, 235),
                Color.FromArgb(255, 220, 180),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(bg, this.ClientRectangle);
            }

            // Заголовок
            string title = "Котик и Рыбки";
            using (var titleFont = new Font("Segoe UI", 36F, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(Color.FromArgb(80, 60, 40)))
            {
                var sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                g.DrawString(title, titleFont, titleBrush,
                    new RectangleF(0, 40, this.ClientSize.Width, 70), sf);
            }

            // Подзаголовок
            string sub = "Собери все рыбки и доберись до двери!";
            using (var subFont = new Font("Segoe UI", 13F))
            using (var subBrush = new SolidBrush(Color.FromArgb(110, 90, 70)))
            {
                var sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                g.DrawString(sub, subFont, subBrush,
                    new RectangleF(0, 120, this.ClientSize.Width, 30), sf);
            }

            // Иллюстрация: котик ловит рыбку
            DrawTitleIllustration(g, animTick);

            // Управление
            string hint = "Управление: ← → — двигаться,  ↑ или Пробел — прыжок";
            using (var hintFont = new Font("Segoe UI", 10F))
            using (var hintBrush = new SolidBrush(Color.FromArgb(100, 80, 60)))
            {
                var sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                g.DrawString(hint, hintFont, hintBrush,
                    new RectangleF(0, 270, this.ClientSize.Width, 30), sf);
            }
        }

        /// <summary>Рисует иллюстрацию на титульном экране: котик и летающая рыбка.</summary>
        private void DrawTitleIllustration(Graphics g, int tick)
        {
            int cx = this.ClientSize.Width / 2;

            // Покачивание рыбки
            double rad = ((tick % 80) / 80.0) * 2.0 * Math.PI;
            int bob = (int)(Math.Sin(rad) * 12.0);

            // Рыбка над котиком
            int fx = cx - 30;
            int fy = 200 + bob;
            DrawFish(g, fx, fy, 26, true);

            // Котик смотрит вверх
            DrawCat(g, cx - 35, 230, 70, Color.FromArgb(255, 170, 80), facingRight: true);
        }

        // ---------- Отрисовка персонажей ----------

        /// <summary>Рисует котика как набор прямоугольников (пиксельный стиль).</summary>
        public static void DrawCat(Graphics g, int x, int y, int size, Color bodyColor, bool facingRight)
        {
            // Координаты в локальной системе (0..size)
            // Тело
            int bodyW = size;
            int bodyH = (int)(size * 0.55);
            int bx = x;
            int by = y + size - bodyH;
            using (var b = new SolidBrush(bodyColor))
                g.FillRectangle(b, bx, by, bodyW, bodyH);

            // Голова
            int headW = (int)(size * 0.7);
            int headH = (int)(size * 0.45);
            int hx = x + (int)(size * 0.15);
            int hy = y + (int)(size * 0.05);
            using (var b = new SolidBrush(bodyColor))
                g.FillRectangle(b, hx, hy, headW, headH);

            // Уши (треугольники)
            Point[] leftEar = {
                new Point(hx, hy),
                new Point(hx + (int)(headW*0.20f), hy - (int)(size*0.18f)),
                new Point(hx + (int)(headW*0.40f), hy)
            };
            Point[] rightEar = {
                new Point(hx + (int)(headW*0.60f), hy),
                new Point(hx + (int)(headW*0.80f), hy - (int)(size*0.18f)),
                new Point(hx + headW, hy)
            };
            g.FillPolygon(new SolidBrush(bodyColor), leftEar);
            g.FillPolygon(new SolidBrush(bodyColor), rightEar);

            // Внутренние уши (розовые)
            Color pink = Color.FromArgb(255, 180, 180);
            Point[] leftInner = {
                new Point(hx + (int)(headW*0.05f), hy - 1),
                new Point(hx + (int)(headW*0.20f), hy - (int)(size*0.10f)),
                new Point(hx + (int)(headW*0.30f), hy - 1)
            };
            Point[] rightInner = {
                new Point(hx + (int)(headW*0.70f), hy - 1),
                new Point(hx + (int)(headW*0.80f), hy - (int)(size*0.10f)),
                new Point(hx + (int)(headW*0.95f), hy - 1)
            };
            g.FillPolygon(new SolidBrush(pink), leftInner);
            g.FillPolygon(new SolidBrush(pink), rightInner);

            // Глаза (в зависимости от направления)
            int eyeY = hy + (int)(headH * 0.35f);
            int eyeOffset = facingRight ? 0 : 0;
            int eyeSize = Math.Max(4, (int)(size * 0.07f));
            int eye1X = hx + (int)(headW * 0.28f) + eyeOffset;
            int eye2X = hx + (int)(headW * 0.62f) + eyeOffset;
            g.FillEllipse(Brushes.White, eye1X, eyeY, eyeSize + 2, eyeSize + 2);
            g.FillEllipse(Brushes.White, eye2X, eyeY, eyeSize + 2, eyeSize + 2);
            // Зрачки смотрят в сторону движения
            int pupilDX = facingRight ? 1 : -1;
            g.FillEllipse(Brushes.Black, eye1X + eyeSize/2 + pupilDX, eyeY + 1, eyeSize, eyeSize);
            g.FillEllipse(Brushes.Black, eye2X + eyeSize/2 + pupilDX, eyeY + 1, eyeSize, eyeSize);

            // Носик (розовый треугольник)
            int noseY = hy + (int)(headH * 0.62f);
            int noseX = hx + (int)(headW * 0.42f);
            Point[] nose = {
                new Point(noseX, noseY),
                new Point(noseX + (int)(headW*0.08f), noseY),
                new Point(noseX + (int)(headW*0.04f), noseY + (int)(size*0.05f))
            };
            g.FillPolygon(new SolidBrush(pink), nose);

            // Полоски (тёмнее основного цвета)
            Color stripe = Color.FromArgb(
                Math.Max(0, bodyColor.R - 50),
                Math.Max(0, bodyColor.G - 40),
                Math.Max(0, bodyColor.B - 30));
            using (var b = new SolidBrush(stripe))
            {
                g.FillRectangle(b, bx + 4, by + 4, 4, bodyH - 8);
                g.FillRectangle(b, bx + bodyW/2 - 2, by + 4, 4, bodyH - 8);
                g.FillRectangle(b, bx + bodyW - 8, by + 4, 4, bodyH - 8);
            }

            // Хвост (изогнутая линия)
            int tailBaseX = facingRight ? bx : bx + bodyW;
            int tailDir = facingRight ? -1 : 1;
            using (var pen = new Pen(bodyColor, 6) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawCurve(pen, new Point[] {
                    new Point(tailBaseX, by + bodyH/2),
                    new Point(tailBaseX + tailDir * 10, by - 4),
                    new Point(tailBaseX + tailDir * 22, by - 14)
                });
            }
        }

        /// <summary>Рисует рыбку (пиксельный стиль).</summary>
        public static void DrawFish(Graphics g, int x, int y, int size, bool facingRight)
        {
            // Тело — эллипс
            Color bodyColor = Color.FromArgb(90, 170, 230);
            Color bellyColor = Color.FromArgb(200, 230, 250);
            int bodyW = size;
            int bodyH = (int)(size * 0.7);

            using (var b = new SolidBrush(bodyColor))
                g.FillEllipse(b, x, y, bodyW, bodyH);
            // Брюшко
            using (var b = new SolidBrush(bellyColor))
                g.FillEllipse(b, x + 2, y + (int)(bodyH * 0.45f), bodyW - 4, (int)(bodyH * 0.5f));

            // Хвост — треугольник
            Point[] tail;
            if (facingRight)
            {
                tail = new Point[] {
                    new Point(x, y + bodyH/2),
                    new Point(x - size/3, y + bodyH/2 - size/4),
                    new Point(x - size/3, y + bodyH/2 + size/4)
                };
            }
            else
            {
                tail = new Point[] {
                    new Point(x + bodyW, y + bodyH/2),
                    new Point(x + bodyW + size/3, y + bodyH/2 - size/4),
                    new Point(x + bodyW + size/3, y + bodyH/2 + size/4)
                };
            }
            g.FillPolygon(new SolidBrush(bodyColor), tail);

            // Глаз
            int eyeX = facingRight ? x + (int)(bodyW * 0.7f) : x + (int)(bodyW * 0.2f);
            int eyeY = y + (int)(bodyH * 0.3f);
            int eyeR = Math.Max(3, size / 8);
            g.FillEllipse(Brushes.White, eyeX, eyeY, eyeR + 1, eyeR + 1);
            g.FillEllipse(Brushes.Black, eyeX + 1, eyeY + 1, eyeR - 1, eyeR - 1);

            // Улыбка (тонкая дуга)
            using (var pen = new Pen(Color.FromArgb(40, 80, 120), 1))
            {
                int mx = facingRight ? x + (int)(bodyW * 0.55f) : x + (int)(bodyW * 0.25f);
                int my = y + (int)(bodyH * 0.55f);
                g.DrawArc(pen, mx, my, size/4, size/4, 20, 140);
            }
        }
    }
}
