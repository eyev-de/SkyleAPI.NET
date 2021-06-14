namespace Skyle
{
    /// <summary>
    /// 2D Point with doubles
    /// </summary>
    public class Point
    {
        /// <summary>
        /// X Coordinate
        /// </summary>
        public double X { get; }

        /// <summary>
        /// Y Coordinate
        /// </summary>
        public double Y { get; }

        internal Point(Skyle_Server.Point p)
        {
            X = p.X;
            Y = p.Y;
        }

        internal Point(Skyle_Server.CalibPoint p)
        {
            X = p.CurrentPoint.X;
            Y = p.CurrentPoint.Y;
        }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public Point(double x, double y)
        {
            X = x;
            Y = y;
        }
    }
}