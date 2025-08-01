﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using NameSpace1;

namespace Microsoft.EntityFrameworkCore.Query
{
    public abstract class AdHocMiscellaneousQueryRelationalTestBase(NonSharedFixture fixture) : AdHocMiscellaneousQueryTestBase(fixture)
    {
        protected TestSqlLoggerFactory TestSqlLoggerFactory
            => (TestSqlLoggerFactory)ListLoggerFactory;

        protected void ClearLog()
            => TestSqlLoggerFactory.Clear();

        protected void AssertSql(params string[] expected)
            => TestSqlLoggerFactory.AssertBaseline(expected);

        protected abstract DbContextOptionsBuilder SetParameterizedCollectionMode(DbContextOptionsBuilder optionsBuilder, ParameterTranslationMode parameterizedCollectionMode);

        #region 2951

        [ConditionalFact]
        public async Task Query_when_null_key_in_database_should_throw()
        {
            var contextFactory = await InitializeAsync<Context2951>(
                onConfiguring: o => o.EnableDetailedErrors(),
                seed: Seed2951);

            using var context = contextFactory.CreateContext();

            Assert.Equal(
                RelationalStrings.ErrorMaterializingPropertyNullReference(nameof(Context2951.ZeroKey2951), "Id", typeof(int)),
                (await Assert.ThrowsAsync<InvalidOperationException>(() => context.ZeroKeys.ToListAsync())).Message);
        }

        protected abstract Task Seed2951(Context2951 context);

        protected class Context2951(DbContextOptions options) : DbContext(options)
        {
            protected override void OnModelCreating(ModelBuilder modelBuilder)
                => modelBuilder.Entity<ZeroKey2951>().ToTable("ZeroKey", t => t.ExcludeFromMigrations())
                    .Property(z => z.Id).ValueGeneratedNever();

            public DbSet<ZeroKey2951> ZeroKeys { get; set; }

            public class ZeroKey2951
            {
                public int Id { get; set; }
            }
        }

        #endregion

        #region 11818

        [ConditionalFact]
        public virtual async Task GroupJoin_Anonymous_projection_GroupBy_Aggregate_join_elimination()
        {
            var contextFactory = await InitializeAsync<Context11818>(
                onConfiguring:
                o => o.ConfigureWarnings(w => w.Log(CoreEventId.FirstWithoutOrderByAndFilterWarning)));

            using (var context = contextFactory.CreateContext())
            {
                var query = (from e in context.Set<Context11818.Entity11818>()
                             join a in context.Set<Context11818.AnotherEntity11818>()
                                 on e.Id equals a.Id into grouping
                             from a in grouping.DefaultIfEmpty()
                             select new { ename = e.Name, aname = a.Name })
                    .GroupBy(g => g.aname)
                    .Select(
                        g => new { g.Key, cnt = g.Count() + 5 })
                    .ToList();

                Assert.Empty(query);
            }

            using (var context = contextFactory.CreateContext())
            {
                var query = (from e in context.Set<Context11818.Entity11818>()
                             join a in context.Set<Context11818.AnotherEntity11818>()
                                 on e.Id equals a.Id into grouping
                             from a in grouping.DefaultIfEmpty()
                             join m in context.Set<Context11818.MaumarEntity11818>()
                                 on e.Id equals m.Id into grouping2
                             from m in grouping2.DefaultIfEmpty()
                             select new { aname = a.Name, mname = m.Name })
                    .GroupBy(
                        g => new { g.aname, g.mname })
                    .Select(
                        g => new { MyKey = g.Key.aname, cnt = g.Count() + 5 })
                    .ToList();

                Assert.Empty(query);
            }

            using (var context = contextFactory.CreateContext())
            {
                var query = (from e in context.Set<Context11818.Entity11818>()
                             join a in context.Set<Context11818.AnotherEntity11818>()
                                 on e.Id equals a.Id into grouping
                             from a in grouping.DefaultIfEmpty()
                             join m in context.Set<Context11818.MaumarEntity11818>()
                                 on e.Id equals m.Id into grouping2
                             from m in grouping2.DefaultIfEmpty()
                             select new { aname = a.Name, mname = m.Name })
                    .OrderBy(g => g.aname)
                    .GroupBy(g => new { g.aname, g.mname })
                    .Select(g => new { MyKey = g.Key.aname, cnt = g.Key.mname }).FirstOrDefault();

                Assert.Null(query);
            }
        }

        protected class Context11818(DbContextOptions options) : DbContext(options)
        {
            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<Entity11818>().ToTable("Table");
                modelBuilder.Entity<AnotherEntity11818>().ToTable("Table");
                modelBuilder.Entity<MaumarEntity11818>().ToTable("Table");

                modelBuilder.Entity<Entity11818>()
                    .HasOne<AnotherEntity11818>()
                    .WithOne()
                    .HasForeignKey<AnotherEntity11818>(b => b.Id);

                modelBuilder.Entity<Entity11818>()
                    .HasOne<MaumarEntity11818>()
                    .WithOne()
                    .HasForeignKey<MaumarEntity11818>(b => b.Id);
            }

            public class Entity11818
            {
                public int Id { get; set; }
                public string Name { get; set; }
            }

            public class AnotherEntity11818
            {
                public int Id { get; set; }
                public string Name { get; set; }
                public bool Exists { get; set; }
            }

            public class MaumarEntity11818
            {
                public int Id { get; set; }
                public string Name { get; set; }
                public bool Exists { get; set; }
            }
        }

        #endregion

        #region 23981

        [ConditionalTheory]
        [MemberData(nameof(IsAsyncData))]
        public virtual async Task Multiple_different_entity_type_from_different_namespaces(bool async)
        {
            var contextFactory = await InitializeAsync<Context23981>();
            using var context = contextFactory.CreateContext();
            //var good1 = context.Set<NameSpace1.TestQuery>().FromSqlRaw(@"SELECT 1 AS MyValue").ToList(); // OK
            //var good2 = context.Set<NameSpace2.TestQuery>().FromSqlRaw(@"SELECT 1 AS MyValue").ToList(); // OK
            var bad = context.Set<TestQuery>().FromSqlRaw(@"SELECT cast(null as int) AS MyValue").ToList(); // Exception
        }

        protected class Context23981(DbContextOptions options) : DbContext(options)
        {
            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                var mb = modelBuilder.Entity(typeof(TestQuery));

                mb.HasBaseType((Type)null);
                mb.HasNoKey();
                mb.ToTable((string)null);

                mb = modelBuilder.Entity(typeof(NameSpace2.TestQuery));

                mb.HasBaseType((Type)null);
                mb.HasNoKey();
                mb.ToTable((string)null);
            }
        }

        #endregion

        #region 27954

        [ConditionalTheory]
        [MemberData(nameof(IsAsyncData))]
        public virtual async Task StoreType_for_UDF_used(bool async)
        {
            var contextFactory = await InitializeAsync<Context27954>();
            using var context = contextFactory.CreateContext();

            var date = new DateTime(2012, 12, 12);
            var query1 = context.Set<Context27954.MyEntity>().Where(x => x.SomeDate == date);
            var query2 = context.Set<Context27954.MyEntity>().Where(x => Context27954.MyEntity.Modify(x.SomeDate) == date);

            if (async)
            {
                await query1.ToListAsync();
                await Assert.ThrowsAnyAsync<Exception>(() => query2.ToListAsync());
            }
            else
            {
                query1.ToList();
                Assert.ThrowsAny<Exception>(() => query2.ToList());
            }
        }

        protected class Context27954(DbContextOptions options) : DbContext(options)
        {
            public DbSet<MyEntity> MyEntities { get; set; }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
                => modelBuilder
                    .HasDbFunction(typeof(MyEntity).GetMethod(nameof(MyEntity.Modify)))
                    .HasName("ModifyDate")
                    .HasStoreType("datetime")
                    .HasSchema("dbo");

            public class MyEntity
            {
                public int Id { get; set; }

                [Column(TypeName = "datetime")]
                public DateTime SomeDate { get; set; }

                public static DateTime Modify(DateTime date)
                    => throw new NotSupportedException();
            }
        }

        #endregion

        #region 34752

        [ConditionalFact]
        public virtual async Task Mapping_JsonElement_property_throws_a_meaningful_exception()
        {
            var message = (await Assert.ThrowsAsync<InvalidOperationException>(
                () => InitializeAsync<Context34752>())).Message;

            Assert.Equal(
                CoreStrings.PropertyNotAdded(nameof(Context34752.Entity), nameof(Context34752.Entity.Json), nameof(JsonElement)),
                message);
        }

        protected class Context34752(DbContextOptions options) : DbContext(options)
        {
            public DbSet<Entity> Entities { get; set; }

            public class Entity
            {
                public int Id { get; set; }
                public JsonElement Json { get; set; }
            }
        }

        #endregion

        #region Inlined redacting

        [ConditionalTheory]
        [MemberData(nameof(InlinedRedactingData))]
        public virtual async Task Check_inlined_constants_redacting(bool async, bool enableSensitiveDataLogging)
        {
            var contextFactory = await InitializeAsync<InlinedRedactingContext>(
                onConfiguring: o =>
                {
                    SetParameterizedCollectionMode(o, ParameterTranslationMode.Constant);
                    o.EnableSensitiveDataLogging(enableSensitiveDataLogging);
                });
            using var context = contextFactory.CreateContext();

            var id = 1;
            var ids = new[] { id, 2, 3 };
            var query1 = context.TestEntities.Where(x => ids.Contains(x.Id));
            var query2 = context.TestEntities.Where(x => ids.Where(y => y == x.Id).Any());
            var query3 = context.TestEntities.Where(x => EF.Constant(id) == x.Id);

            if (async)
            {
                await query1.ToListAsync();
                await query2.ToListAsync();
                await query3.ToListAsync();
            }
            else
            {
                query1.ToList();
                query2.ToList();
                query3.ToList();
            }
        }

        protected class InlinedRedactingContext(DbContextOptions options) : DbContext(options)
        {
            public DbSet<TestEntity> TestEntities { get; set; }

            public class TestEntity
            {
                public int Id { get; set; }
                public string Name { get; set; }
            }
        }

        public static readonly IEnumerable<object[]> InlinedRedactingData = [[true, true], [true, false], [false, true], [false, false]];

        #endregion

        #region 36311

        [ConditionalTheory]
        [MemberData(nameof(IsAsyncData))]
        public async Task Entity_equality_with_Contains_and_Parameter(bool async)
        {
            var contextFactory = await InitializeAsync<Context36311>(
                onConfiguring: o => SetParameterizedCollectionMode(o, ParameterTranslationMode.Parameter));
            using var context = contextFactory.CreateContext();

            List<Context36311.BlogDetails> details = [new Context36311.BlogDetails { Id = 1 }, new Context36311.BlogDetails { Id = 2 }];
            var query = context.Blogs.Where(b => details.Contains(b.Details));

            var result = async
                ? await query.ToListAsync()
                : query.ToList();
        }

        protected class Context36311(DbContextOptions options) : DbContext(options)
        {
            public DbSet<Blog> Blogs { get; set; }

            public class Blog
            {
                public int Id { get; set; }
                public string Name { get; set; }

                public BlogDetails Details { get; set; }
            }

            public class BlogDetails
            {
                public int Id { get; set; }
                public string Name { get; set; }
            }
        }

        #endregion
    }
}

namespace NameSpace1
{
    public class TestQuery
    {
        public int? MyValue { get; set; }
    }
}

namespace NameSpace2
{
    public class TestQuery
    {
        public int MyValue { get; set; }
    }
}
