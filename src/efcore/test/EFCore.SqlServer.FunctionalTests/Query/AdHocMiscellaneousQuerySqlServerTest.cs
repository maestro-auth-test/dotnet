﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// ReSharper disable InconsistentNaming

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal;
using NetTopologySuite.Geometries;

namespace Microsoft.EntityFrameworkCore.Query;

#nullable disable

public class AdHocMiscellaneousQuerySqlServerTest(NonSharedFixture fixture) : AdHocMiscellaneousQueryRelationalTestBase(fixture)
{
    protected override ITestStoreFactory TestStoreFactory
        => SqlServerTestStoreFactory.Instance;

    protected override DbContextOptionsBuilder SetParameterizedCollectionMode(DbContextOptionsBuilder optionsBuilder, ParameterTranslationMode parameterizedCollectionMode)
    {
        new SqlServerDbContextOptionsBuilder(optionsBuilder).UseParameterizedCollectionMode(parameterizedCollectionMode);

        return optionsBuilder;
    }

    protected override Task Seed2951(Context2951 context)
        => context.Database.ExecuteSqlRawAsync(
            """
CREATE TABLE ZeroKey (Id int);
INSERT ZeroKey VALUES (NULL)
""");

    #region 5456

    [ConditionalFact]
    public virtual async Task Include_group_join_is_per_query_context()
    {
        var contextFactory = await InitializeAsync<Context5456>(
            seed: c => c.SeedAsync(),
            createTestStore: () => SqlServerTestStore.Create(StoreName, multipleActiveResultSets: true));

        Parallel.For(
            0, 10, i =>
            {
                using var ctx = contextFactory.CreateContext();
                var result = ctx.Posts.Where(x => x.Blog.Id > 1).Include(x => x.Blog).ToList();

                Assert.Equal(198, result.Count);
            });

        Parallel.For(
            0, 10, i =>
            {
                using var ctx = contextFactory.CreateContext();
                var result = ctx.Posts.Where(x => x.Blog.Id > 1).Include(x => x.Blog).Include(x => x.Comments).ToList();

                Assert.Equal(198, result.Count);
            });

        Parallel.For(
            0, 10, i =>
            {
                using var ctx = contextFactory.CreateContext();
                var result = ctx.Posts.Where(x => x.Blog.Id > 1).Include(x => x.Blog).ThenInclude(b => b.Author).ToList();

                Assert.Equal(198, result.Count);
            });
    }

    [ConditionalFact]
    public virtual async Task Include_group_join_is_per_query_context_async()
    {
        var contextFactory = await InitializeAsync<Context5456>(
            seed: c => c.SeedAsync(),
            createTestStore: () => SqlServerTestStore.Create(StoreName, multipleActiveResultSets: true));

        await Parallel.ForAsync(
            0, 10, async (i, ct) =>
            {
                using var ctx = contextFactory.CreateContext();
                var result = await ctx.Posts.Where(x => x.Blog.Id > 1).Include(x => x.Blog).ToListAsync();

                Assert.Equal(198, result.Count);
            });

        await Parallel.ForAsync(
            0, 10, async (i, ct) =>
            {
                using var ctx = contextFactory.CreateContext();
                var result = await ctx.Posts.Where(x => x.Blog.Id > 1).Include(x => x.Blog).Include(x => x.Comments)
                    .ToListAsync();

                Assert.Equal(198, result.Count);
            });

        await Parallel.ForAsync(
            0, 10, async (i, ct) =>
            {
                using var ctx = contextFactory.CreateContext();
                var result = await ctx.Posts.Where(x => x.Blog.Id > 1).Include(x => x.Blog).ThenInclude(b => b.Author)
                    .ToListAsync();

                Assert.Equal(198, result.Count);
            });
    }

    // Protected so that it can be used by inheriting tests, and so that things like unused setters are not removed.
    protected class Context5456(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Blog> Blogs { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Author> Authors { get; set; }

        public Task SeedAsync()
        {
            for (var i = 0; i < 100; i++)
            {
                Add(
                    new Blog { Posts = [new Post { Comments = [new Comment(), new Comment()] }, new Post()], Author = new Author() });
            }

            return SaveChangesAsync();
        }

        public class Blog
        {
            public int Id { get; set; }
            public List<Post> Posts { get; set; }
            public Author Author { get; set; }
        }

        public class Author
        {
            public int Id { get; set; }
            public List<Blog> Blogs { get; set; }
        }

        public class Post
        {
            public int Id { get; set; }
            public Blog Blog { get; set; }
            public List<Comment> Comments { get; set; }
        }

        public class Comment
        {
            public int Id { get; set; }
            public Post Blog { get; set; }
        }
    }

    #endregion

    #region 8864

    [ConditionalFact]
    public virtual async Task Select_nested_projection()
    {
        var contextFactory = await InitializeAsync<Context8864>(seed: c => c.SeedAsync());

        using (var context = contextFactory.CreateContext())
        {
            var customers = context.Customers
                .Select(c => new { Customer = c, CustomerAgain = Context8864.Get(context, c.Id) })
                .ToList();

            Assert.Equal(2, customers.Count);

            foreach (var customer in customers)
            {
                Assert.Same(customer.Customer, customer.CustomerAgain);
            }
        }

        AssertSql(
            """
SELECT [c].[Id], [c].[Name]
FROM [Customers] AS [c]
""",
            //
            """
@id='1'

SELECT TOP(2) [c].[Id], [c].[Name]
FROM [Customers] AS [c]
WHERE [c].[Id] = @id
""",
            //
            """
@id='2'

SELECT TOP(2) [c].[Id], [c].[Name]
FROM [Customers] AS [c]
WHERE [c].[Id] = @id
""");
    }

    // Protected so that it can be used by inheriting tests, and so that things like unused setters are not removed.
    protected class Context8864(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Customer> Customers { get; set; }

        public Task SeedAsync()
        {
            AddRange(
                new Customer { Name = "Alan" },
                new Customer { Name = "Elon" });

            return SaveChangesAsync();
        }

        public static Customer Get(Context8864 context, int id)
            => context.Customers.Single(c => c.Id == id);

        public class Customer
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }

    #endregion

    #region 9214

    [ConditionalFact]
    public async Task Default_schema_applied_when_no_function_schema()
    {
        var contextFactory = await InitializeAsync<Context9214>(seed: c => c.SeedAsync());

        using (var context = contextFactory.CreateContext())
        {
            var result = context.Widgets.Where(w => w.Val == 1).Select(w => Context9214.AddOne(w.Val)).Single();

            Assert.Equal(2, result);

            AssertSql(
                """
SELECT TOP(2) [foo].[AddOne]([w].[Val])
FROM [foo].[Widgets] AS [w]
WHERE [w].[Val] = 1
""");
        }

        using (var context = contextFactory.CreateContext())
        {
            ClearLog();
            var result = context.Widgets.Where(w => w.Val == 1).Select(w => Context9214.AddTwo(w.Val)).Single();

            Assert.Equal(3, result);

            AssertSql(
                """
SELECT TOP(2) [dbo].[AddTwo]([w].[Val])
FROM [foo].[Widgets] AS [w]
WHERE [w].[Val] = 1
""");
        }
    }

    protected class Context9214(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Widget9214> Widgets { get; set; }

#pragma warning disable IDE0060 // Remove unused parameter
        public static int AddOne(int num)
            => throw new Exception();

        public static int AddTwo(int num)
            => throw new Exception();

        public static int AddThree(int num)
            => throw new Exception();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("foo");

            modelBuilder.Entity<Widget9214>().ToTable("Widgets", "foo");

            modelBuilder.HasDbFunction(typeof(Context9214).GetMethod(nameof(AddOne)));
            modelBuilder.HasDbFunction(typeof(Context9214).GetMethod(nameof(AddTwo))).HasSchema("dbo");
        }

        public async Task SeedAsync()
        {
            var w1 = new Widget9214 { Val = 1 };
            var w2 = new Widget9214 { Val = 2 };
            var w3 = new Widget9214 { Val = 3 };
            Widgets.AddRange(w1, w2, w3);
            await SaveChangesAsync();

            await Database.ExecuteSqlRawAsync(
                """
CREATE FUNCTION foo.AddOne (@num int)
RETURNS int
    AS
BEGIN
    return @num + 1 ;
END
""");

            await Database.ExecuteSqlRawAsync(
                """
CREATE FUNCTION dbo.AddTwo (@num int)
RETURNS int
    AS
BEGIN
    return @num + 2 ;
END
""");
        }

        public class Widget9214
        {
            public int Id { get; set; }
            public int Val { get; set; }
        }
    }

    #endregion

    #region 9277

    [ConditionalFact]
    public virtual async Task From_sql_gets_value_of_out_parameter_in_stored_procedure()
    {
        var contextFactory = await InitializeAsync<Context9277>(seed: c => c.SeedAsync());

        using (var context = contextFactory.CreateContext())
        {
            var valueParam = new SqlParameter
            {
                ParameterName = "Value",
                Value = 0,
                Direction = ParameterDirection.Output,
                SqlDbType = SqlDbType.Int
            };

            Assert.Equal(0, valueParam.Value);

            var blogs = context.Blogs.FromSqlRaw(
                    "[dbo].[GetPersonAndVoteCount]  @id, @Value out",
                    new SqlParameter { ParameterName = "id", Value = 1 },
                    valueParam)
                .ToList();

            Assert.Single(blogs);
            Assert.Equal(1, valueParam.Value);
        }
    }

    protected class Context9277(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Blog9277> Blogs { get; set; }

        public async Task SeedAsync()
        {
            await Database.ExecuteSqlRawAsync(
                """
CREATE PROCEDURE [dbo].[GetPersonAndVoteCount]
 (
    @id int,
    @Value int OUTPUT
)
AS
BEGIN
    SELECT @Value = SomeValue
    FROM dbo.Blogs
    WHERE Id = @id;
    SELECT *
    FROM dbo.Blogs
    WHERE Id = @id;
    END
""");

            AddRange(
                new Blog9277 { SomeValue = 1 },
                new Blog9277 { SomeValue = 2 },
                new Blog9277 { SomeValue = 3 }
            );

            await SaveChangesAsync();
        }

        public class Blog9277
        {
            public int Id { get; set; }
            public int SomeValue { get; set; }
        }
    }

    #endregion

    #region 12482

    [ConditionalFact]
    public virtual async Task Batch_insert_with_sqlvariant_different_types()
    {
        var contextFactory = await InitializeAsync<Context12482>();

        using (var context = contextFactory.CreateContext())
        {
            context.AddRange(
                new Context12482.BaseEntity { Value = 10.0999 },
                new Context12482.BaseEntity { Value = -12345 },
                new Context12482.BaseEntity { Value = "String Value" },
                new Context12482.BaseEntity { Value = new DateTime(2020, 1, 1) });

            context.SaveChanges();

            AssertSql(
                """
@p0='10.0999' (Nullable = true) (DbType = Object)
@p1='-12345' (Nullable = true) (DbType = Object)
@p2='String Value' (Size = 12) (DbType = Object)
@p3='2020-01-01T00:00:00.0000000' (Nullable = true) (DbType = Object)

SET IMPLICIT_TRANSACTIONS OFF;
SET NOCOUNT ON;
MERGE [BaseEntities] USING (
VALUES (@p0, 0),
(@p1, 1),
(@p2, 2),
(@p3, 3)) AS i ([Value], _Position) ON 1=0
WHEN NOT MATCHED THEN
INSERT ([Value])
VALUES (i.[Value])
OUTPUT INSERTED.[Id], i._Position;
""");
        }
    }

    // Protected so that it can be used by inheriting tests, and so that things like unused setters are not removed.
    protected class Context12482(DbContextOptions options) : DbContext(options)
    {
        public virtual DbSet<BaseEntity> BaseEntities { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<BaseEntity>();

        public class BaseEntity
        {
            public int Id { get; set; }

            [Column(TypeName = "sql_variant")]
            public object Value { get; set; }
        }
    }

    #endregion

    #region 12518

    [ConditionalFact]
    public virtual async Task Projecting_entity_with_value_converter_and_include_works()
    {
        var contextFactory = await InitializeAsync<Context12518>(seed: c => c.SeedAsync());
        using var context = contextFactory.CreateContext();
        var result = context.Parents.Include(p => p.Child).OrderBy(e => e.Id).FirstOrDefault();

        AssertSql(
            """
SELECT TOP(1) [p].[Id], [p].[ChildId], [c].[Id], [c].[ParentId], [c].[ULongRowVersion]
FROM [Parents] AS [p]
LEFT JOIN [Children] AS [c] ON [p].[ChildId] = [c].[Id]
ORDER BY [p].[Id]
""");
    }

    [ConditionalFact]
    public virtual async Task Projecting_column_with_value_converter_of_ulong_byte_array()
    {
        var contextFactory = await InitializeAsync<Context12518>(seed: c => c.SeedAsync());
        using var context = contextFactory.CreateContext();
        var result = context.Parents.OrderBy(e => e.Id).Select(p => (ulong?)p.Child.ULongRowVersion).FirstOrDefault();

        AssertSql(
            """
SELECT TOP(1) [c].[ULongRowVersion]
FROM [Parents] AS [p]
LEFT JOIN [Children] AS [c] ON [p].[ChildId] = [c].[Id]
ORDER BY [p].[Id]
""");
    }

    protected class Context12518(DbContextOptions options) : DbContext(options)
    {
        public virtual DbSet<Parent12518> Parents { get; set; }
        public virtual DbSet<Child12518> Children { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var child = modelBuilder.Entity<Child12518>();
            child.HasOne(_ => _.Parent)
                .WithOne(_ => _.Child)
                .HasForeignKey<Parent12518>(_ => _.ChildId);
            child.Property(x => x.ULongRowVersion)
                .HasConversion(new NumberToBytesConverter<ulong>())
                .IsRowVersion()
                .IsRequired()
                .HasColumnType("RowVersion");

            modelBuilder.Entity<Parent12518>();
        }

        public Task SeedAsync()
        {
            Parents.Add(new Parent12518());
            return SaveChangesAsync();
        }

        public class Parent12518
        {
            public Guid Id { get; set; } = Guid.NewGuid();
            public Guid? ChildId { get; set; }
            public Child12518 Child { get; set; }
        }

        public class Child12518
        {
            public Guid Id { get; set; } = Guid.NewGuid();
            public ulong ULongRowVersion { get; set; }
            public Guid ParentId { get; set; }
            public Parent12518 Parent { get; set; }
        }
    }

    #endregion

    #region 13118

    [ConditionalFact]
    public virtual async Task DateTime_Contains_with_smalldatetime_generates_correct_literal()
    {
        var contextFactory = await InitializeAsync<Context13118>(seed: c => c.SeedAsync());
        using var context = contextFactory.CreateContext();
        var testDateList = new List<DateTime> { new(2018, 10, 07) };
        var findRecordsWithDateInList = context.ReproEntity
            .Where(a => testDateList.Contains(a.MyTime))
            .ToList();

        Assert.Single(findRecordsWithDateInList);

        AssertSql(
            """
@testDateList1='2018-10-07T00:00:00.0000000' (DbType = DateTime)

SELECT [r].[Id], [r].[MyTime]
FROM [ReproEntity] AS [r]
WHERE [r].[MyTime] = @testDateList1
""");
    }

    // Protected so that it can be used by inheriting tests, and so that things like unused setters are not removed.
    protected class Context13118(DbContextOptions options) : DbContext(options)
    {
        public virtual DbSet<ReproEntity13118> ReproEntity { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ReproEntity13118>(e => e.Property("MyTime").HasColumnType("smalldatetime"));

        public Task SeedAsync()
        {
            AddRange(
                new ReproEntity13118 { MyTime = new DateTime(2018, 10, 07) },
                new ReproEntity13118 { MyTime = new DateTime(2018, 10, 08) });

            return SaveChangesAsync();
        }
    }

    protected class ReproEntity13118
    {
        public Guid Id { get; set; }
        public DateTime MyTime { get; set; }
    }

    #endregion

    #region 14095

    [ConditionalTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Where_equals_DateTime_Now(bool async)
    {
        var contextFactory = await InitializeAsync<Context14095>(seed: c => c.SeedAsync());

        using var context = contextFactory.CreateContext();
        var query = context.Dates.Where(
            d => d.DateTime2_2 == DateTime.Now
                || d.DateTime2_7 == DateTime.Now
                || d.DateTime == DateTime.Now
                || d.SmallDateTime == DateTime.Now);

        var results = async
            ? await query.ToListAsync()
            : query.ToList();

        Assert.Empty(results);

        AssertSql(
            """
SELECT [d].[Id], [d].[DateTime], [d].[DateTime2], [d].[DateTime2_0], [d].[DateTime2_1], [d].[DateTime2_2], [d].[DateTime2_3], [d].[DateTime2_4], [d].[DateTime2_5], [d].[DateTime2_6], [d].[DateTime2_7], [d].[SmallDateTime]
FROM [Dates] AS [d]
WHERE [d].[DateTime2_2] = GETDATE() OR [d].[DateTime2_7] = GETDATE() OR [d].[DateTime] = GETDATE() OR [d].[SmallDateTime] = GETDATE()
""");
    }

    [ConditionalTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Where_not_equals_DateTime_Now(bool async)
    {
        var contextFactory = await InitializeAsync<Context14095>(seed: c => c.SeedAsync());

        using var context = contextFactory.CreateContext();
        var query = context.Dates.Where(
            d => d.DateTime2_2 != DateTime.Now
                && d.DateTime2_7 != DateTime.Now
                && d.DateTime != DateTime.Now
                && d.SmallDateTime != DateTime.Now);

        var results = async
            ? await query.ToListAsync()
            : query.ToList();

        Assert.Single(results);

        AssertSql(
            """
SELECT [d].[Id], [d].[DateTime], [d].[DateTime2], [d].[DateTime2_0], [d].[DateTime2_1], [d].[DateTime2_2], [d].[DateTime2_3], [d].[DateTime2_4], [d].[DateTime2_5], [d].[DateTime2_6], [d].[DateTime2_7], [d].[SmallDateTime]
FROM [Dates] AS [d]
WHERE [d].[DateTime2_2] <> GETDATE() AND [d].[DateTime2_7] <> GETDATE() AND [d].[DateTime] <> GETDATE() AND [d].[SmallDateTime] <> GETDATE()
""");
    }

    [ConditionalTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Where_equals_new_DateTime(bool async)
    {
        var contextFactory = await InitializeAsync<Context14095>(seed: c => c.SeedAsync());

        using var context = contextFactory.CreateContext();
        var query = context.Dates.Where(
            d => d.SmallDateTime == new DateTime(1970, 9, 3, 12, 0, 0)
                && d.DateTime == new DateTime(1971, 9, 3, 12, 0, 10, 220)
                && d.DateTime2 == new DateTime(1972, 9, 3, 12, 0, 10, 333)
                && d.DateTime2_0 == new DateTime(1973, 9, 3, 12, 0, 10)
                && d.DateTime2_1 == new DateTime(1974, 9, 3, 12, 0, 10, 500)
                && d.DateTime2_2 == new DateTime(1975, 9, 3, 12, 0, 10, 660)
                && d.DateTime2_3 == new DateTime(1976, 9, 3, 12, 0, 10, 777)
                && d.DateTime2_4 == new DateTime(1977, 9, 3, 12, 0, 10, 888)
                && d.DateTime2_5 == new DateTime(1978, 9, 3, 12, 0, 10, 999)
                && d.DateTime2_6 == new DateTime(1979, 9, 3, 12, 0, 10, 111)
                && d.DateTime2_7 == new DateTime(1980, 9, 3, 12, 0, 10, 222));

        var results = async
            ? await query.ToListAsync()
            : query.ToList();

        Assert.Single(results);

        AssertSql(
            """
SELECT [d].[Id], [d].[DateTime], [d].[DateTime2], [d].[DateTime2_0], [d].[DateTime2_1], [d].[DateTime2_2], [d].[DateTime2_3], [d].[DateTime2_4], [d].[DateTime2_5], [d].[DateTime2_6], [d].[DateTime2_7], [d].[SmallDateTime]
FROM [Dates] AS [d]
WHERE [d].[SmallDateTime] = '1970-09-03T12:00:00' AND [d].[DateTime] = '1971-09-03T12:00:10.220' AND [d].[DateTime2] = '1972-09-03T12:00:10.3330000' AND [d].[DateTime2_0] = '1973-09-03T12:00:10' AND [d].[DateTime2_1] = '1974-09-03T12:00:10.5' AND [d].[DateTime2_2] = '1975-09-03T12:00:10.66' AND [d].[DateTime2_3] = '1976-09-03T12:00:10.777' AND [d].[DateTime2_4] = '1977-09-03T12:00:10.8880' AND [d].[DateTime2_5] = '1978-09-03T12:00:10.99900' AND [d].[DateTime2_6] = '1979-09-03T12:00:10.111000' AND [d].[DateTime2_7] = '1980-09-03T12:00:10.2220000'
""");
    }

    [ConditionalTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Where_contains_DateTime_literals(bool async)
    {
        var dateTimes = new[]
        {
            new DateTime(1970, 9, 3, 12, 0, 0),
            new DateTime(1971, 9, 3, 12, 0, 10, 220),
            new DateTime(1972, 9, 3, 12, 0, 10, 333),
            new DateTime(1973, 9, 3, 12, 0, 10),
            new DateTime(1974, 9, 3, 12, 0, 10, 500),
            new DateTime(1975, 9, 3, 12, 0, 10, 660),
            new DateTime(1976, 9, 3, 12, 0, 10, 777),
            new DateTime(1977, 9, 3, 12, 0, 10, 888),
            new DateTime(1978, 9, 3, 12, 0, 10, 999),
            new DateTime(1979, 9, 3, 12, 0, 10, 111),
            new DateTime(1980, 9, 3, 12, 0, 10, 222)
        };

        var contextFactory = await InitializeAsync<Context14095>(seed: c => c.SeedAsync());

        using var context = contextFactory.CreateContext();
        var query = context.Dates.Where(
            d => dateTimes.Contains(d.SmallDateTime)
                && dateTimes.Contains(d.DateTime)
                && dateTimes.Contains(d.DateTime2)
                && dateTimes.Contains(d.DateTime2_0)
                && dateTimes.Contains(d.DateTime2_1)
                && dateTimes.Contains(d.DateTime2_2)
                && dateTimes.Contains(d.DateTime2_3)
                && dateTimes.Contains(d.DateTime2_4)
                && dateTimes.Contains(d.DateTime2_5)
                && dateTimes.Contains(d.DateTime2_6)
                && dateTimes.Contains(d.DateTime2_7));

        var results = async
            ? await query.ToListAsync()
            : query.ToList();

        Assert.Single(results);

        AssertSql(
            """
@dateTimes1='1970-09-03T12:00:00.0000000' (DbType = DateTime)
@dateTimes2='1971-09-03T12:00:10.2200000' (DbType = DateTime)
@dateTimes3='1972-09-03T12:00:10.3330000' (DbType = DateTime)
@dateTimes4='1973-09-03T12:00:10.0000000' (DbType = DateTime)
@dateTimes5='1974-09-03T12:00:10.5000000' (DbType = DateTime)
@dateTimes6='1975-09-03T12:00:10.6600000' (DbType = DateTime)
@dateTimes7='1976-09-03T12:00:10.7770000' (DbType = DateTime)
@dateTimes8='1977-09-03T12:00:10.8880000' (DbType = DateTime)
@dateTimes9='1978-09-03T12:00:10.9990000' (DbType = DateTime)
@dateTimes10='1979-09-03T12:00:10.1110000' (DbType = DateTime)
@dateTimes11='1980-09-03T12:00:10.2220000' (DbType = DateTime)
@dateTimes12='1980-09-03T12:00:10.2220000' (DbType = DateTime)
@dateTimes13='1980-09-03T12:00:10.2220000' (DbType = DateTime)
@dateTimes14='1980-09-03T12:00:10.2220000' (DbType = DateTime)
@dateTimes15='1980-09-03T12:00:10.2220000' (DbType = DateTime)
@dateTimes16='1980-09-03T12:00:10.2220000' (DbType = DateTime)
@dateTimes17='1980-09-03T12:00:10.2220000' (DbType = DateTime)
@dateTimes18='1980-09-03T12:00:10.2220000' (DbType = DateTime)
@dateTimes19='1980-09-03T12:00:10.2220000' (DbType = DateTime)
@dateTimes20='1980-09-03T12:00:10.2220000' (DbType = DateTime)
@dateTimes21='1970-09-03T12:00:00.0000000' (DbType = DateTime)
@dateTimes22='1971-09-03T12:00:10.2200000' (DbType = DateTime)
@dateTimes23='1972-09-03T12:00:10.3330000' (DbType = DateTime)
@dateTimes24='1973-09-03T12:00:10.0000000' (DbType = DateTime)
@dateTimes25='1974-09-03T12:00:10.5000000' (DbType = DateTime)
@dateTimes26='1975-09-03T12:00:10.6600000' (DbType = DateTime)
@dateTimes27='1976-09-03T12:00:10.7770000' (DbType = DateTime)
@dateTimes28='1977-09-03T12:00:10.8880000' (DbType = DateTime)
@dateTimes29='1978-09-03T12:00:10.9990000' (DbType = DateTime)
@dateTimes30='1979-09-03T12:00:10.1110000' (DbType = DateTime)
@dateTimes31='1980-09-03T12:00:10.2220000' (DbType = DateTime)
@dateTimes32='1980-09-03T12:00:10.2220000' (DbType = DateTime)
@dateTimes33='1980-09-03T12:00:10.2220000' (DbType = DateTime)
@dateTimes34='1980-09-03T12:00:10.2220000' (DbType = DateTime)
@dateTimes35='1980-09-03T12:00:10.2220000' (DbType = DateTime)
@dateTimes36='1980-09-03T12:00:10.2220000' (DbType = DateTime)
@dateTimes37='1980-09-03T12:00:10.2220000' (DbType = DateTime)
@dateTimes38='1980-09-03T12:00:10.2220000' (DbType = DateTime)
@dateTimes39='1980-09-03T12:00:10.2220000' (DbType = DateTime)
@dateTimes40='1980-09-03T12:00:10.2220000' (DbType = DateTime)
@dateTimes41='1970-09-03T12:00:00.0000000'
@dateTimes42='1971-09-03T12:00:10.2200000'
@dateTimes43='1972-09-03T12:00:10.3330000'
@dateTimes44='1973-09-03T12:00:10.0000000'
@dateTimes45='1974-09-03T12:00:10.5000000'
@dateTimes46='1975-09-03T12:00:10.6600000'
@dateTimes47='1976-09-03T12:00:10.7770000'
@dateTimes48='1977-09-03T12:00:10.8880000'
@dateTimes49='1978-09-03T12:00:10.9990000'
@dateTimes50='1979-09-03T12:00:10.1110000'
@dateTimes51='1980-09-03T12:00:10.2220000'
@dateTimes52='1980-09-03T12:00:10.2220000'
@dateTimes53='1980-09-03T12:00:10.2220000'
@dateTimes54='1980-09-03T12:00:10.2220000'
@dateTimes55='1980-09-03T12:00:10.2220000'
@dateTimes56='1980-09-03T12:00:10.2220000'
@dateTimes57='1980-09-03T12:00:10.2220000'
@dateTimes58='1980-09-03T12:00:10.2220000'
@dateTimes59='1980-09-03T12:00:10.2220000'
@dateTimes60='1980-09-03T12:00:10.2220000'
@dateTimes61='1970-09-03T12:00:00.0000000'
@dateTimes62='1971-09-03T12:00:10.2200000'
@dateTimes63='1972-09-03T12:00:10.3330000'
@dateTimes64='1973-09-03T12:00:10.0000000'
@dateTimes65='1974-09-03T12:00:10.5000000'
@dateTimes66='1975-09-03T12:00:10.6600000'
@dateTimes67='1976-09-03T12:00:10.7770000'
@dateTimes68='1977-09-03T12:00:10.8880000'
@dateTimes69='1978-09-03T12:00:10.9990000'
@dateTimes70='1979-09-03T12:00:10.1110000'
@dateTimes71='1980-09-03T12:00:10.2220000'
@dateTimes72='1980-09-03T12:00:10.2220000'
@dateTimes73='1980-09-03T12:00:10.2220000'
@dateTimes74='1980-09-03T12:00:10.2220000'
@dateTimes75='1980-09-03T12:00:10.2220000'
@dateTimes76='1980-09-03T12:00:10.2220000'
@dateTimes77='1980-09-03T12:00:10.2220000'
@dateTimes78='1980-09-03T12:00:10.2220000'
@dateTimes79='1980-09-03T12:00:10.2220000'
@dateTimes80='1980-09-03T12:00:10.2220000'
@dateTimes81='1970-09-03T12:00:00.0000000' (Scale = 1)
@dateTimes82='1971-09-03T12:00:10.2200000' (Scale = 1)
@dateTimes83='1972-09-03T12:00:10.3330000' (Scale = 1)
@dateTimes84='1973-09-03T12:00:10.0000000' (Scale = 1)
@dateTimes85='1974-09-03T12:00:10.5000000' (Scale = 1)
@dateTimes86='1975-09-03T12:00:10.6600000' (Scale = 1)
@dateTimes87='1976-09-03T12:00:10.7770000' (Scale = 1)
@dateTimes88='1977-09-03T12:00:10.8880000' (Scale = 1)
@dateTimes89='1978-09-03T12:00:10.9990000' (Scale = 1)
@dateTimes90='1979-09-03T12:00:10.1110000' (Scale = 1)
@dateTimes91='1980-09-03T12:00:10.2220000' (Scale = 1)
@dateTimes92='1980-09-03T12:00:10.2220000' (Scale = 1)
@dateTimes93='1980-09-03T12:00:10.2220000' (Scale = 1)
@dateTimes94='1980-09-03T12:00:10.2220000' (Scale = 1)
@dateTimes95='1980-09-03T12:00:10.2220000' (Scale = 1)
@dateTimes96='1980-09-03T12:00:10.2220000' (Scale = 1)
@dateTimes97='1980-09-03T12:00:10.2220000' (Scale = 1)
@dateTimes98='1980-09-03T12:00:10.2220000' (Scale = 1)
@dateTimes99='1980-09-03T12:00:10.2220000' (Scale = 1)
@dateTimes100='1980-09-03T12:00:10.2220000' (Scale = 1)
@dateTimes101='1970-09-03T12:00:00.0000000' (Scale = 2)
@dateTimes102='1971-09-03T12:00:10.2200000' (Scale = 2)
@dateTimes103='1972-09-03T12:00:10.3330000' (Scale = 2)
@dateTimes104='1973-09-03T12:00:10.0000000' (Scale = 2)
@dateTimes105='1974-09-03T12:00:10.5000000' (Scale = 2)
@dateTimes106='1975-09-03T12:00:10.6600000' (Scale = 2)
@dateTimes107='1976-09-03T12:00:10.7770000' (Scale = 2)
@dateTimes108='1977-09-03T12:00:10.8880000' (Scale = 2)
@dateTimes109='1978-09-03T12:00:10.9990000' (Scale = 2)
@dateTimes110='1979-09-03T12:00:10.1110000' (Scale = 2)
@dateTimes111='1980-09-03T12:00:10.2220000' (Scale = 2)
@dateTimes112='1980-09-03T12:00:10.2220000' (Scale = 2)
@dateTimes113='1980-09-03T12:00:10.2220000' (Scale = 2)
@dateTimes114='1980-09-03T12:00:10.2220000' (Scale = 2)
@dateTimes115='1980-09-03T12:00:10.2220000' (Scale = 2)
@dateTimes116='1980-09-03T12:00:10.2220000' (Scale = 2)
@dateTimes117='1980-09-03T12:00:10.2220000' (Scale = 2)
@dateTimes118='1980-09-03T12:00:10.2220000' (Scale = 2)
@dateTimes119='1980-09-03T12:00:10.2220000' (Scale = 2)
@dateTimes120='1980-09-03T12:00:10.2220000' (Scale = 2)
@dateTimes121='1970-09-03T12:00:00.0000000' (Scale = 3)
@dateTimes122='1971-09-03T12:00:10.2200000' (Scale = 3)
@dateTimes123='1972-09-03T12:00:10.3330000' (Scale = 3)
@dateTimes124='1973-09-03T12:00:10.0000000' (Scale = 3)
@dateTimes125='1974-09-03T12:00:10.5000000' (Scale = 3)
@dateTimes126='1975-09-03T12:00:10.6600000' (Scale = 3)
@dateTimes127='1976-09-03T12:00:10.7770000' (Scale = 3)
@dateTimes128='1977-09-03T12:00:10.8880000' (Scale = 3)
@dateTimes129='1978-09-03T12:00:10.9990000' (Scale = 3)
@dateTimes130='1979-09-03T12:00:10.1110000' (Scale = 3)
@dateTimes131='1980-09-03T12:00:10.2220000' (Scale = 3)
@dateTimes132='1980-09-03T12:00:10.2220000' (Scale = 3)
@dateTimes133='1980-09-03T12:00:10.2220000' (Scale = 3)
@dateTimes134='1980-09-03T12:00:10.2220000' (Scale = 3)
@dateTimes135='1980-09-03T12:00:10.2220000' (Scale = 3)
@dateTimes136='1980-09-03T12:00:10.2220000' (Scale = 3)
@dateTimes137='1980-09-03T12:00:10.2220000' (Scale = 3)
@dateTimes138='1980-09-03T12:00:10.2220000' (Scale = 3)
@dateTimes139='1980-09-03T12:00:10.2220000' (Scale = 3)
@dateTimes140='1980-09-03T12:00:10.2220000' (Scale = 3)
@dateTimes141='1970-09-03T12:00:00.0000000' (Scale = 4)
@dateTimes142='1971-09-03T12:00:10.2200000' (Scale = 4)
@dateTimes143='1972-09-03T12:00:10.3330000' (Scale = 4)
@dateTimes144='1973-09-03T12:00:10.0000000' (Scale = 4)
@dateTimes145='1974-09-03T12:00:10.5000000' (Scale = 4)
@dateTimes146='1975-09-03T12:00:10.6600000' (Scale = 4)
@dateTimes147='1976-09-03T12:00:10.7770000' (Scale = 4)
@dateTimes148='1977-09-03T12:00:10.8880000' (Scale = 4)
@dateTimes149='1978-09-03T12:00:10.9990000' (Scale = 4)
@dateTimes150='1979-09-03T12:00:10.1110000' (Scale = 4)
@dateTimes151='1980-09-03T12:00:10.2220000' (Scale = 4)
@dateTimes152='1980-09-03T12:00:10.2220000' (Scale = 4)
@dateTimes153='1980-09-03T12:00:10.2220000' (Scale = 4)
@dateTimes154='1980-09-03T12:00:10.2220000' (Scale = 4)
@dateTimes155='1980-09-03T12:00:10.2220000' (Scale = 4)
@dateTimes156='1980-09-03T12:00:10.2220000' (Scale = 4)
@dateTimes157='1980-09-03T12:00:10.2220000' (Scale = 4)
@dateTimes158='1980-09-03T12:00:10.2220000' (Scale = 4)
@dateTimes159='1980-09-03T12:00:10.2220000' (Scale = 4)
@dateTimes160='1980-09-03T12:00:10.2220000' (Scale = 4)
@dateTimes161='1970-09-03T12:00:00.0000000' (Scale = 5)
@dateTimes162='1971-09-03T12:00:10.2200000' (Scale = 5)
@dateTimes163='1972-09-03T12:00:10.3330000' (Scale = 5)
@dateTimes164='1973-09-03T12:00:10.0000000' (Scale = 5)
@dateTimes165='1974-09-03T12:00:10.5000000' (Scale = 5)
@dateTimes166='1975-09-03T12:00:10.6600000' (Scale = 5)
@dateTimes167='1976-09-03T12:00:10.7770000' (Scale = 5)
@dateTimes168='1977-09-03T12:00:10.8880000' (Scale = 5)
@dateTimes169='1978-09-03T12:00:10.9990000' (Scale = 5)
@dateTimes170='1979-09-03T12:00:10.1110000' (Scale = 5)
@dateTimes171='1980-09-03T12:00:10.2220000' (Scale = 5)
@dateTimes172='1980-09-03T12:00:10.2220000' (Scale = 5)
@dateTimes173='1980-09-03T12:00:10.2220000' (Scale = 5)
@dateTimes174='1980-09-03T12:00:10.2220000' (Scale = 5)
@dateTimes175='1980-09-03T12:00:10.2220000' (Scale = 5)
@dateTimes176='1980-09-03T12:00:10.2220000' (Scale = 5)
@dateTimes177='1980-09-03T12:00:10.2220000' (Scale = 5)
@dateTimes178='1980-09-03T12:00:10.2220000' (Scale = 5)
@dateTimes179='1980-09-03T12:00:10.2220000' (Scale = 5)
@dateTimes180='1980-09-03T12:00:10.2220000' (Scale = 5)
@dateTimes181='1970-09-03T12:00:00.0000000' (Scale = 6)
@dateTimes182='1971-09-03T12:00:10.2200000' (Scale = 6)
@dateTimes183='1972-09-03T12:00:10.3330000' (Scale = 6)
@dateTimes184='1973-09-03T12:00:10.0000000' (Scale = 6)
@dateTimes185='1974-09-03T12:00:10.5000000' (Scale = 6)
@dateTimes186='1975-09-03T12:00:10.6600000' (Scale = 6)
@dateTimes187='1976-09-03T12:00:10.7770000' (Scale = 6)
@dateTimes188='1977-09-03T12:00:10.8880000' (Scale = 6)
@dateTimes189='1978-09-03T12:00:10.9990000' (Scale = 6)
@dateTimes190='1979-09-03T12:00:10.1110000' (Scale = 6)
@dateTimes191='1980-09-03T12:00:10.2220000' (Scale = 6)
@dateTimes192='1980-09-03T12:00:10.2220000' (Scale = 6)
@dateTimes193='1980-09-03T12:00:10.2220000' (Scale = 6)
@dateTimes194='1980-09-03T12:00:10.2220000' (Scale = 6)
@dateTimes195='1980-09-03T12:00:10.2220000' (Scale = 6)
@dateTimes196='1980-09-03T12:00:10.2220000' (Scale = 6)
@dateTimes197='1980-09-03T12:00:10.2220000' (Scale = 6)
@dateTimes198='1980-09-03T12:00:10.2220000' (Scale = 6)
@dateTimes199='1980-09-03T12:00:10.2220000' (Scale = 6)
@dateTimes200='1980-09-03T12:00:10.2220000' (Scale = 6)
@dateTimes201='1970-09-03T12:00:00.0000000' (Scale = 7)
@dateTimes202='1971-09-03T12:00:10.2200000' (Scale = 7)
@dateTimes203='1972-09-03T12:00:10.3330000' (Scale = 7)
@dateTimes204='1973-09-03T12:00:10.0000000' (Scale = 7)
@dateTimes205='1974-09-03T12:00:10.5000000' (Scale = 7)
@dateTimes206='1975-09-03T12:00:10.6600000' (Scale = 7)
@dateTimes207='1976-09-03T12:00:10.7770000' (Scale = 7)
@dateTimes208='1977-09-03T12:00:10.8880000' (Scale = 7)
@dateTimes209='1978-09-03T12:00:10.9990000' (Scale = 7)
@dateTimes210='1979-09-03T12:00:10.1110000' (Scale = 7)
@dateTimes211='1980-09-03T12:00:10.2220000' (Scale = 7)
@dateTimes212='1980-09-03T12:00:10.2220000' (Scale = 7)
@dateTimes213='1980-09-03T12:00:10.2220000' (Scale = 7)
@dateTimes214='1980-09-03T12:00:10.2220000' (Scale = 7)
@dateTimes215='1980-09-03T12:00:10.2220000' (Scale = 7)
@dateTimes216='1980-09-03T12:00:10.2220000' (Scale = 7)
@dateTimes217='1980-09-03T12:00:10.2220000' (Scale = 7)
@dateTimes218='1980-09-03T12:00:10.2220000' (Scale = 7)
@dateTimes219='1980-09-03T12:00:10.2220000' (Scale = 7)
@dateTimes220='1980-09-03T12:00:10.2220000' (Scale = 7)

SELECT [d].[Id], [d].[DateTime], [d].[DateTime2], [d].[DateTime2_0], [d].[DateTime2_1], [d].[DateTime2_2], [d].[DateTime2_3], [d].[DateTime2_4], [d].[DateTime2_5], [d].[DateTime2_6], [d].[DateTime2_7], [d].[SmallDateTime]
FROM [Dates] AS [d]
WHERE [d].[SmallDateTime] IN (@dateTimes1, @dateTimes2, @dateTimes3, @dateTimes4, @dateTimes5, @dateTimes6, @dateTimes7, @dateTimes8, @dateTimes9, @dateTimes10, @dateTimes11, @dateTimes12, @dateTimes13, @dateTimes14, @dateTimes15, @dateTimes16, @dateTimes17, @dateTimes18, @dateTimes19, @dateTimes20) AND [d].[DateTime] IN (@dateTimes21, @dateTimes22, @dateTimes23, @dateTimes24, @dateTimes25, @dateTimes26, @dateTimes27, @dateTimes28, @dateTimes29, @dateTimes30, @dateTimes31, @dateTimes32, @dateTimes33, @dateTimes34, @dateTimes35, @dateTimes36, @dateTimes37, @dateTimes38, @dateTimes39, @dateTimes40) AND [d].[DateTime2] IN (@dateTimes41, @dateTimes42, @dateTimes43, @dateTimes44, @dateTimes45, @dateTimes46, @dateTimes47, @dateTimes48, @dateTimes49, @dateTimes50, @dateTimes51, @dateTimes52, @dateTimes53, @dateTimes54, @dateTimes55, @dateTimes56, @dateTimes57, @dateTimes58, @dateTimes59, @dateTimes60) AND [d].[DateTime2_0] IN (@dateTimes61, @dateTimes62, @dateTimes63, @dateTimes64, @dateTimes65, @dateTimes66, @dateTimes67, @dateTimes68, @dateTimes69, @dateTimes70, @dateTimes71, @dateTimes72, @dateTimes73, @dateTimes74, @dateTimes75, @dateTimes76, @dateTimes77, @dateTimes78, @dateTimes79, @dateTimes80) AND [d].[DateTime2_1] IN (@dateTimes81, @dateTimes82, @dateTimes83, @dateTimes84, @dateTimes85, @dateTimes86, @dateTimes87, @dateTimes88, @dateTimes89, @dateTimes90, @dateTimes91, @dateTimes92, @dateTimes93, @dateTimes94, @dateTimes95, @dateTimes96, @dateTimes97, @dateTimes98, @dateTimes99, @dateTimes100) AND [d].[DateTime2_2] IN (@dateTimes101, @dateTimes102, @dateTimes103, @dateTimes104, @dateTimes105, @dateTimes106, @dateTimes107, @dateTimes108, @dateTimes109, @dateTimes110, @dateTimes111, @dateTimes112, @dateTimes113, @dateTimes114, @dateTimes115, @dateTimes116, @dateTimes117, @dateTimes118, @dateTimes119, @dateTimes120) AND [d].[DateTime2_3] IN (@dateTimes121, @dateTimes122, @dateTimes123, @dateTimes124, @dateTimes125, @dateTimes126, @dateTimes127, @dateTimes128, @dateTimes129, @dateTimes130, @dateTimes131, @dateTimes132, @dateTimes133, @dateTimes134, @dateTimes135, @dateTimes136, @dateTimes137, @dateTimes138, @dateTimes139, @dateTimes140) AND [d].[DateTime2_4] IN (@dateTimes141, @dateTimes142, @dateTimes143, @dateTimes144, @dateTimes145, @dateTimes146, @dateTimes147, @dateTimes148, @dateTimes149, @dateTimes150, @dateTimes151, @dateTimes152, @dateTimes153, @dateTimes154, @dateTimes155, @dateTimes156, @dateTimes157, @dateTimes158, @dateTimes159, @dateTimes160) AND [d].[DateTime2_5] IN (@dateTimes161, @dateTimes162, @dateTimes163, @dateTimes164, @dateTimes165, @dateTimes166, @dateTimes167, @dateTimes168, @dateTimes169, @dateTimes170, @dateTimes171, @dateTimes172, @dateTimes173, @dateTimes174, @dateTimes175, @dateTimes176, @dateTimes177, @dateTimes178, @dateTimes179, @dateTimes180) AND [d].[DateTime2_6] IN (@dateTimes181, @dateTimes182, @dateTimes183, @dateTimes184, @dateTimes185, @dateTimes186, @dateTimes187, @dateTimes188, @dateTimes189, @dateTimes190, @dateTimes191, @dateTimes192, @dateTimes193, @dateTimes194, @dateTimes195, @dateTimes196, @dateTimes197, @dateTimes198, @dateTimes199, @dateTimes200) AND [d].[DateTime2_7] IN (@dateTimes201, @dateTimes202, @dateTimes203, @dateTimes204, @dateTimes205, @dateTimes206, @dateTimes207, @dateTimes208, @dateTimes209, @dateTimes210, @dateTimes211, @dateTimes212, @dateTimes213, @dateTimes214, @dateTimes215, @dateTimes216, @dateTimes217, @dateTimes218, @dateTimes219, @dateTimes220)
""");
    }

    protected class Context14095(DbContextOptions options) : DbContext(options)
    {
        public DbSet<DatesAndPrunes14095> Dates { get; set; }

        public Task SeedAsync()
        {
            Add(
                new DatesAndPrunes14095
                {
                    SmallDateTime = new DateTime(1970, 9, 3, 12, 0, 0),
                    DateTime = new DateTime(1971, 9, 3, 12, 0, 10, 220),
                    DateTime2 = new DateTime(1972, 9, 3, 12, 0, 10, 333),
                    DateTime2_0 = new DateTime(1973, 9, 3, 12, 0, 10),
                    DateTime2_1 = new DateTime(1974, 9, 3, 12, 0, 10, 500),
                    DateTime2_2 = new DateTime(1975, 9, 3, 12, 0, 10, 660),
                    DateTime2_3 = new DateTime(1976, 9, 3, 12, 0, 10, 777),
                    DateTime2_4 = new DateTime(1977, 9, 3, 12, 0, 10, 888),
                    DateTime2_5 = new DateTime(1978, 9, 3, 12, 0, 10, 999),
                    DateTime2_6 = new DateTime(1979, 9, 3, 12, 0, 10, 111),
                    DateTime2_7 = new DateTime(1980, 9, 3, 12, 0, 10, 222)
                });
            return SaveChangesAsync();
        }

        public class DatesAndPrunes14095
        {
            public int Id { get; set; }

            [Column(TypeName = "smalldatetime")]
            public DateTime SmallDateTime { get; set; }

            [Column(TypeName = "datetime")]
            public DateTime DateTime { get; set; }

            [Column(TypeName = "datetime2")]
            public DateTime DateTime2 { get; set; }

            [Column(TypeName = "datetime2(0)")]
            public DateTime DateTime2_0 { get; set; }

            [Column(TypeName = "datetime2(1)")]
            public DateTime DateTime2_1 { get; set; }

            [Column(TypeName = "datetime2(2)")]
            public DateTime DateTime2_2 { get; set; }

            [Column(TypeName = "datetime2(3)")]
            public DateTime DateTime2_3 { get; set; }

            [Column(TypeName = "datetime2(4)")]
            public DateTime DateTime2_4 { get; set; }

            [Column(TypeName = "datetime2(5)")]
            public DateTime DateTime2_5 { get; set; }

            [Column(TypeName = "datetime2(6)")]
            public DateTime DateTime2_6 { get; set; }

            [Column(TypeName = "datetime2(7)")]
            public DateTime DateTime2_7 { get; set; }
        }
    }

    #endregion

    #region 15518

    [ConditionalTheory]
    [InlineData(false)]
    [InlineData(true)]
    public virtual async Task Nested_queries_does_not_cause_concurrency_exception_sync(bool tracking)
    {
        var contextFactory = await InitializeAsync<Context15518>(seed: c => c.SeedAsync());

        using (var context = contextFactory.CreateContext())
        {
            var query = context.Repos.OrderBy(r => r.Id).Where(r => r.Id > 0);
            query = tracking ? query.AsTracking() : query.AsNoTracking();

            foreach (var a in query)
            {
                foreach (var b in query)
                {
                }
            }
        }

        using (var context = contextFactory.CreateContext())
        {
            var query = context.Repos.OrderBy(r => r.Id).Where(r => r.Id > 0);
            query = tracking ? query.AsTracking() : query.AsNoTracking();

            await foreach (var a in query.AsAsyncEnumerable())
            {
                await foreach (var b in query.AsAsyncEnumerable())
                {
                }
            }
        }

        AssertSql(
            """
SELECT [r].[Id], [r].[Name]
FROM [Repos] AS [r]
WHERE [r].[Id] > 0
ORDER BY [r].[Id]
""",
            //
            """
SELECT [r].[Id], [r].[Name]
FROM [Repos] AS [r]
WHERE [r].[Id] > 0
ORDER BY [r].[Id]
""",
            //
            """
SELECT [r].[Id], [r].[Name]
FROM [Repos] AS [r]
WHERE [r].[Id] > 0
ORDER BY [r].[Id]
""",
            //
            """
SELECT [r].[Id], [r].[Name]
FROM [Repos] AS [r]
WHERE [r].[Id] > 0
ORDER BY [r].[Id]
""",
            //
            """
SELECT [r].[Id], [r].[Name]
FROM [Repos] AS [r]
WHERE [r].[Id] > 0
ORDER BY [r].[Id]
""",
            //
            """
SELECT [r].[Id], [r].[Name]
FROM [Repos] AS [r]
WHERE [r].[Id] > 0
ORDER BY [r].[Id]
""");
    }

    // Protected so that it can be used by inheriting tests, and so that things like unused setters are not removed.
    protected class Context15518(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Repo> Repos { get; set; }

        public Task SeedAsync()
        {
            AddRange(
                new Repo { Name = "London" },
                new Repo { Name = "New York" });

            return SaveChangesAsync();
        }

        public class Repo
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }

    #endregion

    #region 19206

    [ConditionalFact]
    public virtual async Task From_sql_expression_compares_correctly()
    {
        var contextFactory = await InitializeAsync<Context19206>(seed: c => c.SeedAsync());

        using (var context = contextFactory.CreateContext())
        {
            var query = from t1 in context.Tests.FromSqlInterpolated(
                            $"Select * from Tests Where Type = {Context19206.TestType19206.Unit}")
                        from t2 in context.Tests.FromSqlInterpolated(
                            $"Select * from Tests Where Type = {Context19206.TestType19206.Integration}")
                        select new { t1, t2 };

            var result = query.ToList();

            var item = Assert.Single(result);
            Assert.Equal(Context19206.TestType19206.Unit, item.t1.Type);
            Assert.Equal(Context19206.TestType19206.Integration, item.t2.Type);

            AssertSql(
                """
p0='0'
p1='1'

SELECT [m].[Id], [m].[Type], [m0].[Id], [m0].[Type]
FROM (
    Select * from Tests Where Type = @p0
) AS [m]
CROSS JOIN (
    Select * from Tests Where Type = @p1
) AS [m0]
""");
        }
    }

    // Protected so that it can be used by inheriting tests, and so that things like unused setters are not removed.
    protected class Context19206(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Test> Tests { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
        }

        public Task SeedAsync()
        {
            Add(new Test { Type = TestType19206.Unit });
            Add(new Test { Type = TestType19206.Integration });
            return SaveChangesAsync();
        }

        public class Test
        {
            public int Id { get; set; }
            public TestType19206 Type { get; set; }
        }

        public enum TestType19206
        {
            Unit,
            Integration,
        }
    }

    #endregion

    #region 21666

    [ConditionalFact]
    public virtual async Task Thread_safety_in_relational_command_cache()
    {
        var contextFactory = await InitializeAsync<Context21666>(
            onConfiguring: options => ((IDbContextOptionsBuilderInfrastructure)options).AddOrUpdateExtension(
                options.Options.FindExtension<SqlServerOptionsExtension>()
                    .WithConnection(null)
                    .WithConnectionString(SqlServerTestStore.CreateConnectionString(StoreName))));

        var ids = new[] { 1, 2, 3 };

        Parallel.For(
            0, 100,
            i =>
            {
                using var context = contextFactory.CreateContext();
                var query = context.Lists.Where(l => !l.IsDeleted && ids.Contains(l.Id)).ToList();
            });
    }

    // Protected so that it can be used by inheriting tests, and so that things like unused setters are not removed.
    protected class Context21666(DbContextOptions options) : DbContext(options)
    {
        public DbSet<List> Lists { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
        }

        public class List
        {
            public int Id { get; set; }
            public bool IsDeleted { get; set; }
        }
    }

    #endregion

    #region 23282

    [ConditionalFact]
    [SqlServerCondition(SqlServerCondition.SupportsSqlClr)]
    public virtual async Task Can_query_point_with_buffered_data_reader()
    {
        var contextFactory = await InitializeAsync<Context23282>(
            seed: c => c.SeedAsync(),
            onConfiguring: o => new SqlServerDbContextOptionsBuilder(o).UseNetTopologySuite(),
            addServices: c => c.AddEntityFrameworkSqlServerNetTopologySuite());

        using var context = contextFactory.CreateContext();
        var testUser = context.Locations.FirstOrDefault(x => x.Name == "My Location");

        Assert.NotNull(testUser);

        AssertSql(
            """
SELECT TOP(1) [l].[Id], [l].[Name], [l].[Address_County], [l].[Address_Line1], [l].[Address_Line2], [l].[Address_Point], [l].[Address_Postcode], [l].[Address_Town], [l].[Address_Value]
FROM [Locations] AS [l]
WHERE [l].[Name] = N'My Location'
""");
    }

    // Protected so that it can be used by inheriting tests, and so that things like unused setters are not removed.
    protected class Context23282(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Location> Locations { get; set; }

        public Task SeedAsync()
        {
            Locations.Add(
                new Location
                {
                    Name = "My Location",
                    Address = new Address
                    {
                        Line1 = "1 Fake Street",
                        Town = "Fake Town",
                        County = "Fakeshire",
                        Postcode = "PO57 0DE",
                        Point = new Point(115.7930, 37.2431) { SRID = 4326 }
                    }
                });
            return SaveChangesAsync();
        }

        [Owned]
        public class Address
        {
            public string Line1 { get; set; }
            public string Line2 { get; set; }
            public string Town { get; set; }
            public string County { get; set; }
            public string Postcode { get; set; }
            public int Value { get; set; }

            public Point Point { get; set; }
        }

        public class Location
        {
            [Key]
            [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
            public Guid Id { get; set; }

            public string Name { get; set; }
            public Address Address { get; set; }
        }
    }

    #endregion

    #region 24216

    [ConditionalFact]
    public virtual async Task Subquery_take_SelectMany_with_TVF()
    {
        var contextFactory = await InitializeAsync<Context24216>();
        using var context = contextFactory.CreateContext();

        context.Database.ExecuteSqlRaw(
            """
create function [dbo].[GetPersonStatusAsOf] (@personId bigint, @timestamp datetime2)
returns @personStatus table
(
    Id bigint not null,
    PersonId bigint not null,
    GenderId bigint not null,
    StatusMessage nvarchar(max)
)
as
begin
    insert into @personStatus
    select [m].[Id], [m].[PersonId], [m].[PersonId], null
    from [Message] as [m]
    where [m].[PersonId] = @personId and [m].[TimeStamp] = @timestamp
    return
end
""");

        ClearLog();

        var q = from m in context.Message
                orderby m.Id
                select m;

        var q2 =
            from m in q.Take(10)
            from asof in context.GetPersonStatusAsOf(m.PersonId, m.Timestamp)
            select new { Gender = (from g in context.Gender where g.Id == asof.GenderId select g.Description).Single() };

        q2.ToList();

        AssertSql(
            """
@p='10'

SELECT (
    SELECT TOP(1) [g0].[Description]
    FROM [Gender] AS [g0]
    WHERE [g0].[Id] = [g].[GenderId]) AS [Gender]
FROM (
    SELECT TOP(@p) [m].[Id], [m].[PersonId], [m].[Timestamp]
    FROM [Message] AS [m]
    ORDER BY [m].[Id]
) AS [m0]
CROSS APPLY [dbo].[GetPersonStatusAsOf]([m0].[PersonId], [m0].[Timestamp]) AS [g]
ORDER BY [m0].[Id]
""");
    }

    protected class Gender24216
    {
        public long Id { get; set; }

        public string Description { get; set; }
    }

    protected class Message24216
    {
        public long Id { get; set; }

        public long PersonId { get; set; }

        public DateTime Timestamp { get; set; }
    }

    protected class PersonStatus24216
    {
        public long Id { get; set; }

        public long PersonId { get; set; }

        public long GenderId { get; set; }

        public string StatusMessage { get; set; }
    }

    // Protected so that it can be used by inheriting tests, and so that things like unused setters are not removed.
    protected class Context24216(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Gender24216> Gender { get; set; }
        public DbSet<Message24216> Message { get; set; }

        public IQueryable<PersonStatus24216> GetPersonStatusAsOf(long personId, DateTime asOf)
            => FromExpression(() => GetPersonStatusAsOf(personId, asOf));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDbFunction(
                typeof(Context24216).GetMethod(
                    nameof(GetPersonStatusAsOf),
                    [typeof(long), typeof(DateTime)]));
        }
    }

    #endregion

    #region 27427

    [ConditionalTheory]
    [MemberData(nameof(IsAsyncData))]
    public virtual async Task Muliple_occurrences_of_FromSql_in_group_by_aggregate(bool async)
    {
        var contextFactory = await InitializeAsync<Context27427>();
        using var context = contextFactory.CreateContext();
        var query = context.DemoEntities
            .FromSqlRaw("SELECT * FROM DemoEntities WHERE Id = {0}", new SqlParameter { Value = 1 })
            .Select(e => e.Id);

        var query2 = context.DemoEntities
            .Where(e => query.Contains(e.Id))
            .GroupBy(e => e.Id)
            .Select(g => new { g.Key, Aggregate = g.Count() });

        if (async)
        {
            await query2.ToListAsync();
        }
        else
        {
            query2.ToList();
        }

        AssertSql(
            """
p0='1'

SELECT [d].[Id] AS [Key], COUNT(*) AS [Aggregate]
FROM [DemoEntities] AS [d]
WHERE [d].[Id] IN (
    SELECT [m].[Id]
    FROM (
        SELECT * FROM DemoEntities WHERE Id = @p0
    ) AS [m]
)
GROUP BY [d].[Id]
""");
    }

    protected class Context27427(DbContextOptions options) : DbContext(options)
    {
        public DbSet<DemoEntity> DemoEntities { get; set; }
    }

    protected class DemoEntity
    {
        public int Id { get; set; }
    }

    #endregion

    #region 30478

    [ConditionalTheory]
    [MemberData(nameof(IsAsyncData))]
    public virtual async Task TemporalAsOf_with_json_basic_query(bool async)
    {
        var contextFactory = await InitializeAsync<Context30478>(seed: x => x.SeedAsync());
        using var context = contextFactory.CreateContext();
        var query = context.Entities.TemporalAsOf(new DateTime(2010, 1, 1));

        var result = async
            ? await query.ToListAsync()
            : query.ToList();

        Assert.Equal(2, result.Count);
        Assert.True(result.All(x => x.Reference != null));
        Assert.True(result.All(x => x.Collection.Count > 0));

        AssertSql(
            """
SELECT [e].[Id], [e].[Name], [e].[PeriodEnd], [e].[PeriodStart], [e].[Collection], [e].[Reference]
FROM [Entities] FOR SYSTEM_TIME AS OF '2010-01-01T00:00:00.0000000' AS [e]
""");
    }

    [ConditionalTheory]
    [MemberData(nameof(IsAsyncData))]
    public virtual async Task TemporalAll_with_json_basic_query(bool async)
    {
        var contextFactory = await InitializeAsync<Context30478>(seed: x => x.SeedAsync());
        using var context = contextFactory.CreateContext();
        var query = context.Entities.TemporalAll();

        var result = async
            ? await query.ToListAsync()
            : query.ToList();

        Assert.Equal(2, result.Count);
        Assert.True(result.All(x => x.Reference != null));
        Assert.True(result.All(x => x.Collection.Count > 0));

        AssertSql(
            """
SELECT [e].[Id], [e].[Name], [e].[PeriodEnd], [e].[PeriodStart], [e].[Collection], [e].[Reference]
FROM [Entities] FOR SYSTEM_TIME ALL AS [e]
""");
    }

    [ConditionalTheory]
    [MemberData(nameof(IsAsyncData))]
    public virtual async Task TemporalAsOf_project_json_entity_reference(bool async)
    {
        var contextFactory = await InitializeAsync<Context30478>(seed: x => x.SeedAsync());
        using var context = contextFactory.CreateContext();
        var query = context.Entities.TemporalAsOf(new DateTime(2010, 1, 1)).Select(x => x.Reference);

        var result = async
            ? await query.ToListAsync()
            : query.ToList();

        Assert.Equal(2, result.Count);
        Assert.True(result.All(x => x != null));

        AssertSql(
            """
SELECT [e].[Reference], [e].[Id]
FROM [Entities] FOR SYSTEM_TIME AS OF '2010-01-01T00:00:00.0000000' AS [e]
""");
    }

    [ConditionalTheory]
    [MemberData(nameof(IsAsyncData))]
    public virtual async Task TemporalAsOf_project_json_entity_collection(bool async)
    {
        var contextFactory = await InitializeAsync<Context30478>(seed: x => x.SeedAsync());
        using var context = contextFactory.CreateContext();
        var query = context.Entities.TemporalAsOf(new DateTime(2010, 1, 1)).Select(x => x.Collection);

        var result = async
            ? await query.ToListAsync()
            : query.ToList();

        Assert.Equal(2, result.Count);
        Assert.True(result.All(x => x.Count > 0));

        AssertSql(
            """
SELECT [e].[Collection], [e].[Id]
FROM [Entities] FOR SYSTEM_TIME AS OF '2010-01-01T00:00:00.0000000' AS [e]
""");
    }

    protected class Context30478(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Entity30478> Entities { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Entity30478>().Property(x => x.Id).ValueGeneratedNever();
            modelBuilder.Entity<Entity30478>().ToTable("Entities", tb => tb.IsTemporal());
            modelBuilder.Entity<Entity30478>().OwnsOne(
                x => x.Reference, nb =>
                {
                    nb.ToJson();
                    nb.OwnsOne(x => x.Nested);
                });

            modelBuilder.Entity<Entity30478>().OwnsMany(
                x => x.Collection, nb =>
                {
                    nb.ToJson();
                    nb.OwnsOne(x => x.Nested);
                });
        }

        public async Task SeedAsync()
        {
            var e1 = new Entity30478
            {
                Id = 1,
                Name = "e1",
                Reference = new Json30478 { Name = "r1", Nested = new JsonNested30478 { Number = 1 } },
                Collection =
                [
                    new Json30478 { Name = "c11", Nested = new JsonNested30478 { Number = 11 } },

                    new Json30478 { Name = "c12", Nested = new JsonNested30478 { Number = 12 } },

                    new Json30478 { Name = "c13", Nested = new JsonNested30478 { Number = 12 } }
                ]
            };

            var e2 = new Entity30478
            {
                Id = 2,
                Name = "e2",
                Reference = new Json30478 { Name = "r2", Nested = new JsonNested30478 { Number = 2 } },
                Collection =
                [
                    new Json30478 { Name = "c21", Nested = new JsonNested30478 { Number = 21 } },

                    new Json30478 { Name = "c22", Nested = new JsonNested30478 { Number = 22 } }
                ]
            };

            AddRange(e1, e2);
            await SaveChangesAsync();

            RemoveRange(e1, e2);
            await SaveChangesAsync();

            await Database.ExecuteSqlRawAsync("ALTER TABLE [Entities] SET (SYSTEM_VERSIONING = OFF)");
            await Database.ExecuteSqlRawAsync("ALTER TABLE [Entities] DROP PERIOD FOR SYSTEM_TIME");

            await Database.ExecuteSqlRawAsync("UPDATE [EntitiesHistory] SET PeriodStart = '2000-01-01T01:00:00.0000000Z'");
            await Database.ExecuteSqlRawAsync("UPDATE [EntitiesHistory] SET PeriodEnd = '2020-07-01T07:00:00.0000000Z'");

            await Database.ExecuteSqlRawAsync("ALTER TABLE [Entities] ADD PERIOD FOR SYSTEM_TIME ([PeriodStart], [PeriodEnd])");
            await Database.ExecuteSqlRawAsync(
                "ALTER TABLE [Entities] SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [dbo].[EntitiesHistory]))");
        }
    }

    protected class Entity30478
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public Json30478 Reference { get; set; }
        public List<Json30478> Collection { get; set; }
    }

    protected class Json30478
    {
        public string Name { get; set; }
        public JsonNested30478 Nested { get; set; }
    }

    protected class JsonNested30478
    {
        public int Number { get; set; }
    }

    #endregion

    public override async Task First_FirstOrDefault_ix_async()
    {
        await base.First_FirstOrDefault_ix_async();

        AssertSql(
            """
SELECT TOP(1) [p].[Id], [p].[Name]
FROM [Products] AS [p]
ORDER BY [p].[Id]
""",
            //
            """
@p0='1'

SET IMPLICIT_TRANSACTIONS OFF;
SET NOCOUNT ON;
DELETE FROM [Products]
OUTPUT 1
WHERE [Id] = @p0;
""",
            //
            """
@p0='Product 1' (Size = 4000)

SET IMPLICIT_TRANSACTIONS OFF;
SET NOCOUNT ON;
INSERT INTO [Products] ([Name])
OUTPUT INSERTED.[Id]
VALUES (@p0);
""",
            //
            """
SELECT TOP(1) [p].[Id], [p].[Name]
FROM [Products] AS [p]
ORDER BY [p].[Id]
""",
            //
            """
@p0='2'

SET IMPLICIT_TRANSACTIONS OFF;
SET NOCOUNT ON;
DELETE FROM [Products]
OUTPUT 1
WHERE [Id] = @p0;
""");
    }

    public override async Task Discriminator_type_is_handled_correctly()
    {
        await base.Discriminator_type_is_handled_correctly();

        AssertSql(
            """
SELECT [p].[Id], [p].[Discriminator], [p].[Name]
FROM [Products] AS [p]
WHERE [p].[Discriminator] = 1
""",
            //
            """
SELECT [p].[Id], [p].[Discriminator], [p].[Name]
FROM [Products] AS [p]
WHERE [p].[Discriminator] = 1
""");
    }

    public override async Task New_instances_in_projection_are_not_shared_across_results()
    {
        await base.New_instances_in_projection_are_not_shared_across_results();

        AssertSql(
            """
SELECT [p].[Id], [p].[BlogId], [p].[Title]
FROM [Posts] AS [p]
""");
    }

    public override async Task Enum_has_flag_applies_explicit_cast_for_constant()
    {
        await base.Enum_has_flag_applies_explicit_cast_for_constant();

        AssertSql(
            """
SELECT [e].[Id], [e].[Permission], [e].[PermissionByte], [e].[PermissionShort]
FROM [Entities] AS [e]
WHERE [e].[Permission] & CAST(17179869184 AS bigint) = CAST(17179869184 AS bigint)
""",
            //
            """
SELECT [e].[Id], [e].[Permission], [e].[PermissionByte], [e].[PermissionShort]
FROM [Entities] AS [e]
WHERE [e].[PermissionShort] & CAST(4 AS smallint) = CAST(4 AS smallint)
""");
    }

    public override async Task Enum_has_flag_does_not_apply_explicit_cast_for_non_constant()
    {
        await base.Enum_has_flag_does_not_apply_explicit_cast_for_non_constant();

        AssertSql(
            """
SELECT [e].[Id], [e].[Permission], [e].[PermissionByte], [e].[PermissionShort]
FROM [Entities] AS [e]
WHERE [e].[Permission] & [e].[Permission] = [e].[Permission]
""",
            //
            """
SELECT [e].[Id], [e].[Permission], [e].[PermissionByte], [e].[PermissionShort]
FROM [Entities] AS [e]
WHERE [e].[PermissionByte] & [e].[PermissionByte] = [e].[PermissionByte]
""");
    }

    public override async Task Variable_from_closure_is_parametrized()
    {
        await base.Variable_from_closure_is_parametrized();

        AssertSql(
            """
@id='1'

SELECT [e].[Id], [e].[Name]
FROM [Entities] AS [e]
WHERE [e].[Id] = @id
""",
            //
            """
@id='2'

SELECT [e].[Id], [e].[Name]
FROM [Entities] AS [e]
WHERE [e].[Id] = @id
""",
            //
            """
@id='1'

SELECT [e].[Id], [e].[Name]
FROM [Entities] AS [e]
WHERE [e].[Id] = @id
""",
            //
            """
@id='2'

SELECT [e].[Id], [e].[Name]
FROM [Entities] AS [e]
WHERE [e].[Id] = @id
""",
            //
            """
@id='1'

SELECT [e].[Id], [e].[Name]
FROM [Entities] AS [e]
WHERE [e].[Id] IN (
    SELECT [e0].[Id]
    FROM [Entities] AS [e0]
    WHERE [e0].[Id] = @id
)
""",
            //
            """
@id='2'

SELECT [e].[Id], [e].[Name]
FROM [Entities] AS [e]
WHERE [e].[Id] IN (
    SELECT [e0].[Id]
    FROM [Entities] AS [e0]
    WHERE [e0].[Id] = @id
)
""");
    }

    public override async Task Relational_command_cache_creates_new_entry_when_parameter_nullability_changes()
    {
        await base.Relational_command_cache_creates_new_entry_when_parameter_nullability_changes();

        AssertSql(
            """
@name='A' (Size = 4000)

SELECT [e].[Id], [e].[Name]
FROM [Entities] AS [e]
WHERE [e].[Name] = @name
""",
            //
            """
SELECT [e].[Id], [e].[Name]
FROM [Entities] AS [e]
WHERE [e].[Name] IS NULL
""");
    }

    public override async Task Query_cache_entries_are_evicted_as_necessary()
    {
        await base.Query_cache_entries_are_evicted_as_necessary();

        AssertSql();
    }

    public override async Task Explicitly_compiled_query_does_not_add_cache_entry()
    {
        await base.Explicitly_compiled_query_does_not_add_cache_entry();

        AssertSql(
            """
SELECT TOP(2) [e].[Id], [e].[Name]
FROM [Entities] AS [e]
WHERE [e].[Id] = 1
""");
    }

    public override async Task Conditional_expression_with_conditions_does_not_collapse_if_nullable_bool()
    {
        await base.Conditional_expression_with_conditions_does_not_collapse_if_nullable_bool();

        AssertSql(
            """
SELECT CASE
    WHEN [c0].[Id] IS NOT NULL THEN ~[c0].[Processed]
END AS [Processing]
FROM [Carts] AS [c]
LEFT JOIN [Configuration] AS [c0] ON [c].[ConfigurationId] = [c0].[Id]
""");
    }

    public override async Task QueryBuffer_requirement_is_computed_when_querying_base_type_while_derived_type_has_shadow_prop()
    {
        await base.QueryBuffer_requirement_is_computed_when_querying_base_type_while_derived_type_has_shadow_prop();

        AssertSql(
            """
SELECT [b].[Id], [b].[IsTwo], [b].[MoreStuffId]
FROM [Bases] AS [b]
""");
    }

    public override async Task Average_with_cast()
    {
        await base.Average_with_cast();

        AssertSql(
            """
SELECT [p].[Id], [p].[DecimalColumn], [p].[DoubleColumn], [p].[FloatColumn], [p].[IntColumn], [p].[LongColumn], [p].[NullableDecimalColumn], [p].[NullableDoubleColumn], [p].[NullableFloatColumn], [p].[NullableIntColumn], [p].[NullableLongColumn], [p].[Price]
FROM [Prices] AS [p]
""",
            //
            """
SELECT AVG([p].[Price])
FROM [Prices] AS [p]
""",
            //
            """
SELECT AVG(CAST([p].[IntColumn] AS float))
FROM [Prices] AS [p]
""",
            //
            """
SELECT AVG(CAST([p].[NullableIntColumn] AS float))
FROM [Prices] AS [p]
""",
            //
            """
SELECT AVG(CAST([p].[LongColumn] AS float))
FROM [Prices] AS [p]
""",
            //
            """
SELECT AVG(CAST([p].[NullableLongColumn] AS float))
FROM [Prices] AS [p]
""",
            //
            """
SELECT CAST(AVG([p].[FloatColumn]) AS real)
FROM [Prices] AS [p]
""",
            //
            """
SELECT CAST(AVG([p].[NullableFloatColumn]) AS real)
FROM [Prices] AS [p]
""",
            //
            """
SELECT AVG([p].[DoubleColumn])
FROM [Prices] AS [p]
""",
            //
            """
SELECT AVG([p].[NullableDoubleColumn])
FROM [Prices] AS [p]
""",
            //
            """
SELECT AVG([p].[DecimalColumn])
FROM [Prices] AS [p]
""",
            //
            """
SELECT AVG([p].[NullableDecimalColumn])
FROM [Prices] AS [p]
""");
    }

    public override async Task Parameterless_ctor_on_inner_DTO_gets_called_for_every_row()
    {
        await base.Parameterless_ctor_on_inner_DTO_gets_called_for_every_row();

        AssertSql(
            """
SELECT [e].[Id], [e].[Name]
FROM [Entities] AS [e]
""");
    }

    public override async Task Union_and_insert_works_correctly_together()
    {
        await base.Union_and_insert_works_correctly_together();

        AssertSql(
            """
@id1='1'
@id2='2'

SELECT [t].[Id]
FROM [Tables1] AS [t]
WHERE [t].[Id] = @id1
UNION
SELECT [t0].[Id]
FROM [Tables2] AS [t0]
WHERE [t0].[Id] = @id2
""",
            //
            """
SET NOCOUNT ON;
INSERT INTO [Tables1]
OUTPUT INSERTED.[Id]
DEFAULT VALUES;
INSERT INTO [Tables1]
OUTPUT INSERTED.[Id]
DEFAULT VALUES;
INSERT INTO [Tables2]
OUTPUT INSERTED.[Id]
DEFAULT VALUES;
INSERT INTO [Tables2]
OUTPUT INSERTED.[Id]
DEFAULT VALUES;
""");
    }

    public override async Task Repeated_parameters_in_generated_query_sql()
    {
        await base.Repeated_parameters_in_generated_query_sql();

        AssertSql(
            """
@k='1'

SELECT TOP(1) [a].[Id], [a].[Name]
FROM [Autos] AS [a]
WHERE [a].[Id] = @k
""",
            //
            """
@p='2'

SELECT TOP(1) [a].[Id], [a].[Name]
FROM [Autos] AS [a]
WHERE [a].[Id] = @p
""",
            //
            """
@entity_equality_a_Id='1' (Nullable = true)
@entity_equality_b_Id='2' (Nullable = true)

SELECT [e].[Id], [e].[AnotherAutoId], [e].[AutoId]
FROM [EqualAutos] AS [e]
LEFT JOIN [Autos] AS [a] ON [e].[AutoId] = [a].[Id]
LEFT JOIN [Autos] AS [a0] ON [e].[AnotherAutoId] = [a0].[Id]
WHERE ([a].[Id] = @entity_equality_a_Id AND [a0].[Id] = @entity_equality_b_Id) OR ([a].[Id] = @entity_equality_b_Id AND [a0].[Id] = @entity_equality_a_Id)
""");
    }

    public override async Task Operators_combine_nullability_of_entity_shapers()
    {
        await base.Operators_combine_nullability_of_entity_shapers();

        AssertSql(
            """
SELECT [a].[Id], [a].[a], [a].[a1], [a].[forkey], [b].[Id] AS [Id0], [b].[b], [b].[b1], [b].[forkey] AS [forkey0]
FROM [As] AS [a]
LEFT JOIN [Bs] AS [b] ON [a].[forkey] = [b].[forkey]
UNION ALL
SELECT [a0].[Id], [a0].[a], [a0].[a1], [a0].[forkey], [b0].[Id] AS [Id0], [b0].[b], [b0].[b1], [b0].[forkey] AS [forkey0]
FROM [Bs] AS [b0]
LEFT JOIN [As] AS [a0] ON [b0].[forkey] = [a0].[forkey]
WHERE [a0].[Id] IS NULL
""",
            //
            """
SELECT [a].[Id], [a].[a], [a].[a1], [a].[forkey], [b].[Id] AS [Id0], [b].[b], [b].[b1], [b].[forkey] AS [forkey0]
FROM [As] AS [a]
LEFT JOIN [Bs] AS [b] ON [a].[forkey] = [b].[forkey]
UNION
SELECT [a0].[Id], [a0].[a], [a0].[a1], [a0].[forkey], [b0].[Id] AS [Id0], [b0].[b], [b0].[b1], [b0].[forkey] AS [forkey0]
FROM [Bs] AS [b0]
LEFT JOIN [As] AS [a0] ON [b0].[forkey] = [a0].[forkey]
WHERE [a0].[Id] IS NULL
""",
            //
            """
SELECT [a].[Id], [a].[a], [a].[a1], [a].[forkey], [b].[Id] AS [Id0], [b].[b], [b].[b1], [b].[forkey] AS [forkey0]
FROM [As] AS [a]
LEFT JOIN [Bs] AS [b] ON [a].[forkey] = [b].[forkey]
EXCEPT
SELECT [a0].[Id], [a0].[a], [a0].[a1], [a0].[forkey], [b0].[Id] AS [Id0], [b0].[b], [b0].[b1], [b0].[forkey] AS [forkey0]
FROM [Bs] AS [b0]
LEFT JOIN [As] AS [a0] ON [b0].[forkey] = [a0].[forkey]
""",
            //
            """
SELECT [a].[Id], [a].[a], [a].[a1], [a].[forkey], [b].[Id] AS [Id0], [b].[b], [b].[b1], [b].[forkey] AS [forkey0]
FROM [As] AS [a]
LEFT JOIN [Bs] AS [b] ON [a].[forkey] = [b].[forkey]
INTERSECT
SELECT [a0].[Id], [a0].[a], [a0].[a1], [a0].[forkey], [b0].[Id] AS [Id0], [b0].[b], [b0].[b1], [b0].[forkey] AS [forkey0]
FROM [Bs] AS [b0]
LEFT JOIN [As] AS [a0] ON [b0].[forkey] = [a0].[forkey]
""");
    }

    public override async Task Shadow_property_with_inheritance()
    {
        await base.Shadow_property_with_inheritance();

        AssertSql(
            """
SELECT [c].[Id], [c].[Discriminator], [c].[IsPrimary], [c].[UserName], [c].[EmployerId], [c].[ServiceOperatorId]
FROM [Contacts] AS [c]
""",
            //
            """
SELECT [c].[Id], [c].[Discriminator], [c].[IsPrimary], [c].[UserName], [c].[ServiceOperatorId], [s].[Id]
FROM [Contacts] AS [c]
INNER JOIN [ServiceOperators] AS [s] ON [c].[ServiceOperatorId] = [s].[Id]
WHERE [c].[Discriminator] = N'ServiceOperatorContact'
""",
            //
            """
SELECT [c].[Id], [c].[Discriminator], [c].[IsPrimary], [c].[UserName], [c].[ServiceOperatorId]
FROM [Contacts] AS [c]
WHERE [c].[Discriminator] = N'ServiceOperatorContact'
""");
    }

    public override async Task Inlined_dbcontext_is_not_leaking()
    {
        await base.Inlined_dbcontext_is_not_leaking();

        AssertSql(
            """
SELECT [b].[Id]
FROM [Blogs] AS [b]
""");
    }

    public override async Task GroupJoin_Anonymous_projection_GroupBy_Aggregate_join_elimination()
    {
        await base.GroupJoin_Anonymous_projection_GroupBy_Aggregate_join_elimination();

        AssertSql(
            """
SELECT [t1].[AnotherEntity11818_Name] AS [Key], COUNT(*) + 5 AS [cnt]
FROM [Table] AS [t]
LEFT JOIN (
    SELECT [t0].[Id], [t0].[Exists], [t0].[AnotherEntity11818_Name]
    FROM [Table] AS [t0]
    WHERE [t0].[Exists] IS NOT NULL
) AS [t1] ON [t].[Id] = CASE
    WHEN [t1].[Exists] IS NOT NULL THEN [t1].[Id]
END
GROUP BY [t1].[AnotherEntity11818_Name]
""",
            //
            """
SELECT [t1].[AnotherEntity11818_Name] AS [MyKey], COUNT(*) + 5 AS [cnt]
FROM [Table] AS [t]
LEFT JOIN (
    SELECT [t0].[Id], [t0].[Exists], [t0].[AnotherEntity11818_Name]
    FROM [Table] AS [t0]
    WHERE [t0].[Exists] IS NOT NULL
) AS [t1] ON [t].[Id] = CASE
    WHEN [t1].[Exists] IS NOT NULL THEN [t1].[Id]
END
LEFT JOIN (
    SELECT [t2].[Id], [t2].[MaumarEntity11818_Exists], [t2].[MaumarEntity11818_Name]
    FROM [Table] AS [t2]
    WHERE [t2].[MaumarEntity11818_Exists] IS NOT NULL
) AS [t3] ON [t].[Id] = CASE
    WHEN [t3].[MaumarEntity11818_Exists] IS NOT NULL THEN [t3].[Id]
END
GROUP BY [t1].[AnotherEntity11818_Name], [t3].[MaumarEntity11818_Name]
""",
            //
            """
SELECT TOP(1) [t1].[AnotherEntity11818_Name] AS [MyKey], [t3].[MaumarEntity11818_Name] AS [cnt]
FROM [Table] AS [t]
LEFT JOIN (
    SELECT [t0].[Id], [t0].[Exists], [t0].[AnotherEntity11818_Name]
    FROM [Table] AS [t0]
    WHERE [t0].[Exists] IS NOT NULL
) AS [t1] ON [t].[Id] = CASE
    WHEN [t1].[Exists] IS NOT NULL THEN [t1].[Id]
END
LEFT JOIN (
    SELECT [t2].[Id], [t2].[MaumarEntity11818_Exists], [t2].[MaumarEntity11818_Name]
    FROM [Table] AS [t2]
    WHERE [t2].[MaumarEntity11818_Exists] IS NOT NULL
) AS [t3] ON [t].[Id] = CASE
    WHEN [t3].[MaumarEntity11818_Exists] IS NOT NULL THEN [t3].[Id]
END
GROUP BY [t1].[AnotherEntity11818_Name], [t3].[MaumarEntity11818_Name]
""");
    }

    public override async Task LeftJoin_with_missing_key_values_on_both_sides(bool async)
    {
        await base.LeftJoin_with_missing_key_values_on_both_sides(async);

        AssertSql(
            """
SELECT [c].[CustomerID], [c].[CustomerName], CASE
    WHEN [p].[PostcodeID] IS NULL THEN ''
    ELSE [p].[TownName]
END AS [TownName], CASE
    WHEN [p].[PostcodeID] IS NULL THEN ''
    ELSE [p].[PostcodeValue]
END AS [PostcodeValue]
FROM [Customers] AS [c]
LEFT JOIN [Postcodes] AS [p] ON [c].[PostcodeID] = [p].[PostcodeID]
""");
    }

    public override async Task Comparing_enum_casted_to_byte_with_int_parameter(bool async)
    {
        await base.Comparing_enum_casted_to_byte_with_int_parameter(async);

        AssertSql(
            """
@bitterTaste='1'

SELECT [i].[IceCreamId], [i].[Name], [i].[Taste]
FROM [IceCreams] AS [i]
WHERE [i].[Taste] = @bitterTaste
""");
    }

    public override async Task Comparing_enum_casted_to_byte_with_int_constant(bool async)
    {
        await base.Comparing_enum_casted_to_byte_with_int_constant(async);

        AssertSql(
            """
SELECT [i].[IceCreamId], [i].[Name], [i].[Taste]
FROM [IceCreams] AS [i]
WHERE [i].[Taste] = 1
""");
    }

    public override async Task Comparing_byte_column_to_enum_in_vb_creating_double_cast(bool async)
    {
        await base.Comparing_byte_column_to_enum_in_vb_creating_double_cast(async);

        AssertSql(
            """
SELECT [f].[Id], [f].[Taste]
FROM [Foods] AS [f]
WHERE [f].[Taste] = CAST(1 AS tinyint)
""");
    }

    public override async Task Null_check_removal_in_ternary_maintain_appropriate_cast(bool async)
    {
        await base.Null_check_removal_in_ternary_maintain_appropriate_cast(async);

        AssertSql(
            """
SELECT CAST([f].[Taste] AS tinyint) AS [Bar]
FROM [Foods] AS [f]
""");
    }

    public override async Task SaveChangesAsync_accepts_changes_with_ConfigureAwait_true()
    {
        await base.SaveChangesAsync_accepts_changes_with_ConfigureAwait_true();

        AssertSql(
            """
SET IMPLICIT_TRANSACTIONS OFF;
SET NOCOUNT ON;
INSERT INTO [ObservableThings]
OUTPUT INSERTED.[Id]
DEFAULT VALUES;
""");
    }

    public override async Task Bool_discriminator_column_works(bool async)
    {
        await base.Bool_discriminator_column_works(async);

        AssertSql(
            """
SELECT [a].[Id], [a].[BlogId], [b].[Id], [b].[IsPhotoBlog], [b].[Title], [b].[NumberOfPhotos]
FROM [Authors] AS [a]
LEFT JOIN [Blog] AS [b] ON [a].[BlogId] = [b].[Id]
""");
    }

    public override async Task Multiple_different_entity_type_from_different_namespaces(bool async)
    {
        await base.Multiple_different_entity_type_from_different_namespaces(async);

        AssertSql(
            """
SELECT cast(null as int) AS MyValue
""");
    }

    public override async Task Unwrap_convert_node_over_projection_when_translating_contains_over_subquery(bool async)
    {
        await base.Unwrap_convert_node_over_projection_when_translating_contains_over_subquery(async);

        AssertSql(
            """
@currentUserId='1'

SELECT CASE
    WHEN [u].[Id] IN (
        SELECT [u0].[Id]
        FROM [Memberships] AS [m]
        INNER JOIN [Users] AS [u0] ON [m].[UserId] = [u0].[Id]
        WHERE [m].[GroupId] IN (
            SELECT [m0].[GroupId]
            FROM [Memberships] AS [m0]
            WHERE [m0].[UserId] = @currentUserId
        )
    ) THEN CAST(1 AS bit)
    ELSE CAST(0 AS bit)
END AS [HasAccess]
FROM [Users] AS [u]
""");
    }

    public override async Task Unwrap_convert_node_over_projection_when_translating_contains_over_subquery_2(bool async)
    {
        await base.Unwrap_convert_node_over_projection_when_translating_contains_over_subquery_2(async);

        AssertSql(
            """
@currentUserId='1'

SELECT CASE
    WHEN [u].[Id] IN (
        SELECT [u0].[Id]
        FROM [Memberships] AS [m]
        INNER JOIN [Groups] AS [g] ON [m].[GroupId] = [g].[Id]
        INNER JOIN [Users] AS [u0] ON [m].[UserId] = [u0].[Id]
        WHERE [g].[Id] IN (
            SELECT [g0].[Id]
            FROM [Memberships] AS [m0]
            INNER JOIN [Groups] AS [g0] ON [m0].[GroupId] = [g0].[Id]
            WHERE [m0].[UserId] = @currentUserId
        )
    ) THEN CAST(1 AS bit)
    ELSE CAST(0 AS bit)
END AS [HasAccess]
FROM [Users] AS [u]
""");
    }

    public override async Task Unwrap_convert_node_over_projection_when_translating_contains_over_subquery_3(bool async)
    {
        await base.Unwrap_convert_node_over_projection_when_translating_contains_over_subquery_3(async);

        AssertSql(
            """
@currentUserId='1'

SELECT CASE
    WHEN EXISTS (
        SELECT 1
        FROM [Memberships] AS [m]
        INNER JOIN [Users] AS [u0] ON [m].[UserId] = [u0].[Id]
        WHERE [m].[GroupId] IN (
            SELECT [m0].[GroupId]
            FROM [Memberships] AS [m0]
            WHERE [m0].[UserId] = @currentUserId
        ) AND [u0].[Id] = [u].[Id]) THEN CAST(1 AS bit)
    ELSE CAST(0 AS bit)
END AS [HasAccess]
FROM [Users] AS [u]
""");
    }

    public override async Task GroupBy_aggregate_on_right_side_of_join(bool async)
    {
        await base.GroupBy_aggregate_on_right_side_of_join(async);

        AssertSql(
            """
@orderId='123456'

SELECT [o].[Id], [o].[CancellationDate], [o].[OrderId], [o].[ShippingDate]
FROM [OrderItems] AS [o]
INNER JOIN (
    SELECT [o0].[OrderId] AS [Key]
    FROM [OrderItems] AS [o0]
    WHERE [o0].[OrderId] = @orderId
    GROUP BY [o0].[OrderId]
) AS [o1] ON [o].[OrderId] = [o1].[Key]
WHERE [o].[OrderId] = @orderId
ORDER BY [o].[OrderId]
""");
    }

    public override async Task Enum_with_value_converter_matching_take_value(bool async)
    {
        await base.Enum_with_value_converter_matching_take_value(async);

        AssertSql(
            """
@orderItemType='MyType1' (Nullable = false) (Size = 4000)
@p='1'

SELECT [o1].[Id], COALESCE((
    SELECT TOP(1) [o3].[Price]
    FROM [OrderItems] AS [o3]
    WHERE [o1].[Id] = [o3].[OrderId] AND [o3].[Type] = @orderItemType), 0.0E0) AS [SpecialSum]
FROM (
    SELECT TOP(@p) [o].[Id]
    FROM [Orders] AS [o]
    WHERE EXISTS (
        SELECT 1
        FROM [OrderItems] AS [o0]
        WHERE [o].[Id] = [o0].[OrderId])
    ORDER BY [o].[Id]
) AS [o2]
INNER JOIN [Orders] AS [o1] ON [o2].[Id] = [o1].[Id]
ORDER BY [o2].[Id]
""");
    }

    public override async Task GroupBy_Aggregate_over_navigations_repeated(bool async)
    {
        await base.GroupBy_Aggregate_over_navigations_repeated(async);

        AssertSql(
            """
SELECT (
    SELECT MIN([o].[HourlyRate])
    FROM [TimeSheets] AS [t0]
    LEFT JOIN [Order] AS [o] ON [t0].[OrderId] = [o].[Id]
    WHERE [t0].[OrderId] IS NOT NULL AND [t].[OrderId] = [t0].[OrderId]) AS [HourlyRate], (
    SELECT MIN([c].[Id])
    FROM [TimeSheets] AS [t1]
    INNER JOIN [Project] AS [p] ON [t1].[ProjectId] = [p].[Id]
    INNER JOIN [Customers] AS [c] ON [p].[CustomerId] = [c].[Id]
    WHERE [t1].[OrderId] IS NOT NULL AND [t].[OrderId] = [t1].[OrderId]) AS [CustomerId], (
    SELECT MIN([c0].[Name])
    FROM [TimeSheets] AS [t2]
    INNER JOIN [Project] AS [p0] ON [t2].[ProjectId] = [p0].[Id]
    INNER JOIN [Customers] AS [c0] ON [p0].[CustomerId] = [c0].[Id]
    WHERE [t2].[OrderId] IS NOT NULL AND [t].[OrderId] = [t2].[OrderId]) AS [CustomerName]
FROM [TimeSheets] AS [t]
WHERE [t].[OrderId] IS NOT NULL
GROUP BY [t].[OrderId]
""");
    }

    public override async Task Aggregate_over_subquery_in_group_by_projection(bool async)
    {
        await base.Aggregate_over_subquery_in_group_by_projection(async);

        AssertSql(
            """
SELECT [o].[CustomerId], (
    SELECT MIN([o0].[HourlyRate])
    FROM [Order] AS [o0]
    WHERE [o0].[CustomerId] = [o].[CustomerId]) AS [CustomerMinHourlyRate], MIN([o].[HourlyRate]) AS [HourlyRate], COUNT(*) AS [Count]
FROM [Order] AS [o]
WHERE [o].[Number] <> N'A1' OR [o].[Number] IS NULL
GROUP BY [o].[CustomerId], [o].[Number]
""");
    }

    public override async Task Aggregate_over_subquery_in_group_by_projection_2(bool async)
    {
        await base.Aggregate_over_subquery_in_group_by_projection_2(async);

        AssertSql(
            """
SELECT [t].[Value] AS [A], (
    SELECT MAX([t0].[Id])
    FROM [Tables] AS [t0]
    WHERE [t0].[Value] = MAX([t].[Id]) * 6 OR ([t0].[Value] IS NULL AND MAX([t].[Id]) IS NULL)) AS [B]
FROM [Tables] AS [t]
GROUP BY [t].[Value]
""");
    }

    public override async Task Group_by_aggregate_in_subquery_projection_after_group_by(bool async)
    {
        await base.Group_by_aggregate_in_subquery_projection_after_group_by(async);

        AssertSql(
            """
SELECT [t].[Value] AS [A], COALESCE(SUM([t].[Id]), 0) AS [B], COALESCE((
    SELECT TOP(1) COALESCE(SUM([t].[Id]), 0) + COALESCE(SUM([t0].[Id]), 0)
    FROM [Tables] AS [t0]
    GROUP BY [t0].[Value]
    ORDER BY (SELECT 1)), 0) AS [C]
FROM [Tables] AS [t]
GROUP BY [t].[Value]
""");
    }

    public override async Task Subquery_first_member_compared_to_null(bool async)
    {
        await base.Subquery_first_member_compared_to_null(async);

        AssertSql(
            """
SELECT (
    SELECT TOP(1) [c1].[SomeOtherNullableDateTime]
    FROM [Child] AS [c1]
    WHERE [p].[Id] = [c1].[ParentId] AND [c1].[SomeNullableDateTime] IS NULL
    ORDER BY [c1].[SomeInteger])
FROM [Parents] AS [p]
WHERE EXISTS (
    SELECT 1
    FROM [Child] AS [c]
    WHERE [p].[Id] = [c].[ParentId] AND [c].[SomeNullableDateTime] IS NULL) AND (
    SELECT TOP(1) [c0].[SomeOtherNullableDateTime]
    FROM [Child] AS [c0]
    WHERE [p].[Id] = [c0].[ParentId] AND [c0].[SomeNullableDateTime] IS NULL
    ORDER BY [c0].[SomeInteger]) IS NOT NULL
""");
    }

    public override async Task SelectMany_where_Select(bool async)
    {
        await base.SelectMany_where_Select(async);

        AssertSql(
            """
SELECT [c1].[SomeNullableDateTime]
FROM [Parents] AS [p]
INNER JOIN (
    SELECT [c0].[ParentId], [c0].[SomeNullableDateTime], [c0].[SomeOtherNullableDateTime]
    FROM (
        SELECT [c].[ParentId], [c].[SomeNullableDateTime], [c].[SomeOtherNullableDateTime], ROW_NUMBER() OVER(PARTITION BY [c].[ParentId] ORDER BY [c].[SomeInteger]) AS [row]
        FROM [Child] AS [c]
        WHERE [c].[SomeNullableDateTime] IS NULL
    ) AS [c0]
    WHERE [c0].[row] <= 1
) AS [c1] ON [p].[Id] = [c1].[ParentId]
WHERE [c1].[SomeOtherNullableDateTime] IS NOT NULL
""");
    }

    public override async Task Flattened_GroupJoin_on_interface_generic(bool async)
    {
        await base.Flattened_GroupJoin_on_interface_generic(async);

        AssertSql(
            """
SELECT [c].[Id], [c].[ParentId], [c].[SomeInteger], [c].[SomeNullableDateTime], [c].[SomeOtherNullableDateTime]
FROM [Parents] AS [p]
LEFT JOIN [Child] AS [c] ON [p].[Id] = [c].[Id]
""");
    }

    public override async Task StoreType_for_UDF_used(bool async)
    {
        await base.StoreType_for_UDF_used(async);

        AssertSql(
            """
@date='2012-12-12T00:00:00.0000000' (DbType = DateTime)

SELECT [m].[Id], [m].[SomeDate]
FROM [MyEntities] AS [m]
WHERE [m].[SomeDate] = @date
""",
            //
            """
@date='2012-12-12T00:00:00.0000000' (DbType = DateTime)

SELECT [m].[Id], [m].[SomeDate]
FROM [MyEntities] AS [m]
WHERE [dbo].[ModifyDate]([m].[SomeDate]) = @date
""");
    }

    public override async Task Pushdown_does_not_add_grouping_key_to_projection_when_distinct_is_applied(bool async)
    {
        await base.Pushdown_does_not_add_grouping_key_to_projection_when_distinct_is_applied(async);

        AssertSql(
            """
@p='123456'

SELECT TOP(@p) [t].[JSON]
FROM [TableDatas] AS [t]
INNER JOIN (
    SELECT DISTINCT [i].[Parcel]
    FROM [IndexDatas] AS [i]
    WHERE [i].[Parcel] = N'some condition'
    GROUP BY [i].[Parcel], [i].[RowId]
    HAVING COUNT(*) = 1
) AS [i0] ON [t].[ParcelNumber] = [i0].[Parcel]
WHERE [t].[TableId] = 123
ORDER BY [t].[ParcelNumber]
""");
    }

    public override async Task Filter_on_nested_DTO_with_interface_gets_simplified_correctly(bool async)
    {
        await base.Filter_on_nested_DTO_with_interface_gets_simplified_correctly(async);

        AssertSql(
            """
SELECT [c].[Id], [c].[CompanyId], CASE
    WHEN [c0].[Id] IS NOT NULL THEN CAST(1 AS bit)
    ELSE CAST(0 AS bit)
END, [c0].[Id], [c0].[CompanyName], [c0].[CountryId], [c1].[Id], [c1].[CountryName]
FROM [Customers] AS [c]
LEFT JOIN [Companies] AS [c0] ON [c].[CompanyId] = [c0].[Id]
LEFT JOIN [Countries] AS [c1] ON [c0].[CountryId] = [c1].[Id]
WHERE CASE
    WHEN [c0].[Id] IS NOT NULL THEN [c1].[CountryName]
END = N'COUNTRY'
""");
    }

    public override async Task Check_inlined_constants_redacting(bool async, bool enableSensitiveDataLogging)
    {
        await base.Check_inlined_constants_redacting(async, enableSensitiveDataLogging);

        if (!enableSensitiveDataLogging)
        {
            AssertSql(
                """
SELECT [t].[Id], [t].[Name]
FROM [TestEntities] AS [t]
WHERE [t].[Id] IN (?, ?, ?)
""",
                //
                """
SELECT [t].[Id], [t].[Name]
FROM [TestEntities] AS [t]
WHERE EXISTS (
    SELECT 1
    FROM (VALUES (CAST(? AS int)), (?), (?)) AS [i]([Value])
    WHERE [i].[Value] = [t].[Id])
""",
                //
                """
SELECT [t].[Id], [t].[Name]
FROM [TestEntities] AS [t]
WHERE ? = [t].[Id]
""");
        }
        else
        {
            AssertSql(
                """
SELECT [t].[Id], [t].[Name]
FROM [TestEntities] AS [t]
WHERE [t].[Id] IN (1, 2, 3)
""",
                //
                """
SELECT [t].[Id], [t].[Name]
FROM [TestEntities] AS [t]
WHERE EXISTS (
    SELECT 1
    FROM (VALUES (CAST(1 AS int)), (2), (3)) AS [i]([Value])
    WHERE [i].[Value] = [t].[Id])
""",
            //
            """
SELECT [t].[Id], [t].[Name]
FROM [TestEntities] AS [t]
WHERE 1 = [t].[Id]
""");
        }
    }
}
