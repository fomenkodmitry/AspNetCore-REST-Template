﻿using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Domain.Base;

namespace Infrastructure.Repositories.Generic
{
    public interface IGenericRepository
    {
        IQueryable<TEntity> Get<TEntity>() where TEntity : BaseModel;
        IQueryable<TEntity> Get<TEntity>(Func<TEntity, bool> predicate) where TEntity : BaseModel;
        Task<TEntity> GetById<TEntity>(Guid id) where TEntity : BaseModel;

        Task<TEntity> Create<TEntity>(TEntity item) where TEntity : BaseModel;

        Task Update<TEntity>(TEntity item) where TEntity : BaseModel;

        Task Remove<TEntity>(TEntity item) where TEntity : BaseModel;

        IQueryable<TEntity> GetWithInclude<TEntity>(params Expression<Func<TEntity, object>>[] includeProperties)
            where TEntity : BaseModel;

        TEntity GetOne<TEntity>(Func<TEntity, bool> predicate) where TEntity : BaseModel;

        IQueryable<TEntity> GetWithInclude<TEntity>(Func<TEntity, bool> predicate,
            params Expression<Func<TEntity, object>>[] includeProperties) where TEntity : BaseModel;
    }
}