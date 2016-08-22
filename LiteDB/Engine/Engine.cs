﻿using System;

namespace LiteDB
{
    /// <summary>
    /// A internal class that take care of all engine data structure access - it´s basic implementation of a NoSql database
    /// Its isolated from complete solution - works on low level only
    /// </summary>
    internal partial class Engine : IDisposable
    {
        #region Services instances

        private Logger _log;

        private IDiskService _disk;

        private PageService _pager;

        private TransactionService _trans;

        private IndexService _indexer;

        private DataService _data;

        private CollectionService _collections;

        public Engine(IDiskService disk)
        {
            _disk = disk;
            _log = new Logger();

            // open datafile (or create)
            _disk.Open();

            if (_disk.IsJournalEnabled)
            {
                // try recovery if has journal file
                _disk.Recovery();
            }

            // initialize all services
            _pager = new PageService(_disk);
            _indexer = new IndexService(_pager);
            _data = new DataService(_pager);
            _trans = new TransactionService(_disk, _pager);
            _collections = new CollectionService(_pager, _indexer, _data, _trans);
        }

        #endregion Services instances

        public Logger Log { get { return _log; } }

        /// <summary>
        /// Get the collection page only when nedded. Gets from pager always to garantee that wil be the last (in case of clear cache will get a new one - pageID never changes)
        /// </summary>
        private CollectionPage GetCollectionPage(string name, bool addIfNotExits)
        {
            if (name == null) return null;

            // search my page on collection service
            var col = _collections.Get(name);

            if (col == null && addIfNotExits)
            {
                _log.Write(Logger.COMMAND, "create new collection '{0}'", name);

                col = _collections.Add(name);
            }

            return col;
        }

        /// <summary>
        /// Encapsulate all write transaction operation
        /// </summary>
        private T Transaction<T>(string colName, bool addIfNotExists, Func<CollectionPage, T> action)
        {
            lock (_disk)
            {
                try
                {
                    var col = this.GetCollectionPage(colName, addIfNotExists);

                    var result = action(col);

                    _trans.Commit();

                    return result;
                }
                catch (Exception ex)
                {
                    _log.Write(Logger.ERROR, ex.Message);
                    _trans.Rollback();
                    throw;
                }
            }
        }

        public void Dispose()
        {
            _disk.Dispose();
        }
    }
}