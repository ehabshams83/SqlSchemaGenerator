using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Syn.Core.SqlSchemaGenerator.Models
{
    /// <summary>
    /// Enum representing referential actions for foreign key constraints.
    /// </summary>
    public enum ReferentialAction
    {
        /// <summary>
        /// No action is taken on delete or update.
        /// </summary>
        NoAction,

        /// <summary>
        /// Deletes or updates cascade to the referencing rows.
        /// </summary>
        Cascade,

        /// <summary>
        /// Sets referencing column to NULL on delete or update.
        /// </summary>
        SetNull,

        /// <summary>
        /// Sets referencing column to default value on delete or update.
        /// </summary>
        SetDefault,


        Restrict
    }
}
