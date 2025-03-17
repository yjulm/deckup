/*
 * 创作者：yjulm@hotmail.com
 * 生成时间：2021/3/27 15:53:32
 * CLR版本：4.0.30319.42000
 */

using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Deckup.Extend
{
    public static class ExpressionExt
    {
        public static string GetMemberName<T>(this Expression<Func<T>> expression)
        {
            MemberInfo info;
            if (!expression.IsMemberExpression(out info))
                throw new ArgumentException(string.Format("invalid expression!, expression:{0}"
                    , expression == null ? "Null" : expression.ToString()));

            return info.Name;
        }

        public static bool IsMemberExpression<T>(this Expression<Func<T>> expression, out MemberInfo info)
        {
            bool ret = expression != null && expression.Body is MemberExpression;
            info = ret ? ((MemberExpression)expression.Body).Member : null;
            return ret;
        }
    }
}